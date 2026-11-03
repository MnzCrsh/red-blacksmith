using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using ClickHouse.Driver;
using ClickHouse.Driver.ADO.Parameters;
using ClickHouse.Driver.Utility;
using Microsoft.Extensions.Logging;
using RedBlacksmith.TrafficProcessing.Db.Models;

namespace RedBlacksmith.TrafficProcessing.Db;

/// <summary>
/// Последовательно применяет SQL-миграции к ClickHouse.
/// </summary>
/// TODO: https://github.com/MnzCrsh/red-blacksmith/issues/8 Вынести Migration Runner из TrafficProcessing сервиса в отдельный микросервис
internal class ClickhouseMigrationRunner(
    ClickHouseClient client,
    ClickHouseOptions options,
    ILogger<ClickhouseMigrationRunner> logger)
{
    private const string DatabaseToken = "{{database}}";
    
    private static readonly Regex MigrationFileNameRegex = 
        new(@"^(?<version>\d{12})_(?<name>[A-Za-z0-9_]+)\.sql$", RegexOptions.Compiled | RegexOptions.CultureInvariant);
    
    /// <summary>
    /// Создаёт служебную инфраструктуру и применяет неприменённые миграции.
    /// </summary>
    public async Task RunAsync(CancellationToken cancellationToken)
    {

        await EnsureInfrastructureAsync(cancellationToken);
        var appliedMigrations = await LoadAppliedMigrationsAsync(cancellationToken);

        var migrations = LoadMigrations();
        EnsureMigrationFilesExist(migrations, appliedMigrations);

        foreach (var migration in migrations)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (appliedMigrations.TryGetValue(migration.Version, out var applied))
            {
                ValidateAppliedMigration(migration, applied);
                continue;
            }

            logger.LogInformation("Applying ClickHouse migration {Version}: {Name}.", migration.Version, migration.Name);

            var sql = migration.Sql.Replace(DatabaseToken, $"`{options.Database}`", StringComparison.Ordinal);

            await client.ExecuteNonQueryAsync(sql, cancellationToken: cancellationToken);

            await SaveMigrationAsync(migration, cancellationToken);

            logger.LogInformation("Applied ClickHouse migration {Version}: {Name}.", migration.Version, migration.Name);
        }

        logger.LogInformation("ClickHouse migrations completed. Applied migrations: {Count}.", migrations.Count);
    }

    /// <summary>
    /// Создаёт базу данных и таблицу истории миграций, если еще не созданы.
    /// </summary>
    private async Task EnsureInfrastructureAsync(
        CancellationToken cancellationToken)
    {
        var database = $"`{options.Database}`";

        await client.ExecuteNonQueryAsync($"CREATE DATABASE IF NOT EXISTS {database}", cancellationToken: cancellationToken);

        await client.ExecuteNonQueryAsync(
            $"""
             CREATE TABLE IF NOT EXISTS {database}.schema_migrations
             (
                 version UInt64,
                 name String,
                 checksum FixedString(64),
                 applied_at_utc DateTime64(3, 'UTC') DEFAULT now64(3, 'UTC')
             )
             ENGINE = MergeTree
             ORDER BY version
             """, cancellationToken: cancellationToken);
    }

    /// <summary>
    /// Загружает применённые миграции из ClickHouse.
    /// </summary>
    private async Task<Dictionary<ulong, AppliedMigration>>
        LoadAppliedMigrationsAsync(CancellationToken cancellationToken)
    {
        var sql = $"""
                   SELECT version, name, checksum
                   FROM `{options.Database}`.schema_migrations
                   ORDER BY version
                   """;

        await using var reader = await client.ExecuteReaderAsync(sql, cancellationToken: cancellationToken);

        var migrations = new Dictionary<ulong, AppliedMigration>();

        while (reader.Read())
        {
            var migration = new AppliedMigration(reader.GetFieldValue<ulong>(0), reader.GetString(1), reader.GetString(2));

            if (migrations.TryGetValue(migration.Version, out var existing))
            {
                if (existing != migration)
                    throw new InvalidOperationException($"Conflicting ClickHouse migration history for version {migration.Version}.");

                continue;
            }

            migrations.Add(migration.Version, migration);
        }

        return migrations;
    }

    /// <summary>
    /// Сохраняет информацию об успешно применённой миграции.
    /// </summary>
    private async Task SaveMigrationAsync(
        Migration migration,
        CancellationToken cancellationToken)
    {
        var database = $"`{options.Database}`";

        var parameters = new ClickHouseParameterCollection();

        parameters.AddParameter("version", migration.Version);
        parameters.AddParameter("name", migration.Name);
        parameters.AddParameter("checksum", migration.Checksum);

        var sql = $$"""
                    INSERT INTO {{database}}.schema_migrations
                    (version, name, checksum)
                    VALUES
                    ({version:UInt64}, {name:String}, {checksum:FixedString(64)})
                    """;

        await client.ExecuteNonQueryAsync(sql, parameters, cancellationToken: cancellationToken);
    }

    /// <summary>
    /// Загружает SQL-миграции из ресурсов сборки.
    /// </summary>
    private static IReadOnlyList<Migration> LoadMigrations()
    {
        var assembly = typeof(ClickhouseMigrationRunner).Assembly;
        var resourceNames = assembly.GetManifestResourceNames();

        var migrations = new List<Migration>();

        foreach (var resourceName in resourceNames)
        {
            var match = MigrationFileNameRegex.Match(resourceName);

            if (!match.Success)
            {
                continue;
            }

            using var stream = assembly.GetManifestResourceStream(resourceName) ??
                               throw new InvalidOperationException($"Unable to load migration resource '{resourceName}'.");

            using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);

            var sql = reader.ReadToEnd()
                            .Replace("\r\n", "\n", StringComparison.Ordinal)
                            .Replace('\r', '\n')
                            .Trim();

            if (string.IsNullOrWhiteSpace(sql))
            {
                throw new InvalidOperationException($"Migration '{resourceName}' is empty.");
            }

            var version = ulong.Parse(match.Groups["version"].Value, CultureInfo.InvariantCulture);

            var name = match.Groups["name"].Value;

            var checksum = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(sql)))
                                  .ToLowerInvariant();

            migrations.Add(new Migration(version, name, sql, checksum));
        }

        if (migrations.Count == 0)
        {
            throw new InvalidOperationException(
                "No embedded ClickHouse SQL migrations were found.");
        }

        migrations.Sort(
            static (left, right) => left.Version.CompareTo(right.Version));

        for (var i = 1; i < migrations.Count; i++)
        {
            if (migrations[i - 1].Version == migrations[i].Version)
            {
                throw new InvalidOperationException(
                    $"Duplicate ClickHouse migration version: {migrations[i].Version}.");
            }
        }

        return migrations;
    }

    /// <summary>
    /// Проверяет наличие исходных файлов всех ранее применённых миграций.
    /// </summary>
    private static void EnsureMigrationFilesExist(
        IReadOnlyList<Migration> migrations,
        IReadOnlyDictionary<ulong, AppliedMigration> appliedMigrations)
    {
        var availableVersions = migrations
            .Select(static migration => migration.Version)
            .ToHashSet();

        foreach (var applied in appliedMigrations.Values)
        {
            if (!availableVersions.Contains(applied.Version))
            {
                throw new InvalidOperationException($"Applied ClickHouse migration {applied.Version} is missing from the assembly.");
            }
        }
    }

    /// <summary>
    /// Проверяет соответствие файла уже применённой миграции.
    /// </summary>
    private static void ValidateAppliedMigration(
        Migration migration,
        AppliedMigration applied)
    {
        if (migration.Name != applied.Name || migration.Checksum != applied.Checksum)
        {
            throw new InvalidOperationException(
                $"Applied ClickHouse migration {migration.Version} was modified. Create a new migration instead of changing an existing one.");
        }
    }

    private sealed record Migration(ulong Version, string Name, string Sql, string Checksum);
    private sealed record AppliedMigration(ulong Version, string Name, string Checksum);
}
