using Dapper;
using Npgsql;

namespace RedBlacksmith.TrafficProcessing.Postgres;

/// <summary>
/// Обновляет публичные Materialized View.
/// </summary>
public class MaterializedViewRefreshService(NpgsqlDataSource dataSource)
{
    /// <summary>
    /// Пересчитывает минутную статистику сетевого трафика.
    /// </summary>
    public async Task RefreshSourceTrafficMinuteAsync(
        CancellationToken cancellationToken)
    {
        await using var connection =
            await dataSource.OpenConnectionAsync(cancellationToken);

        await connection.ExecuteAsync(new CommandDefinition("SELECT traffic_read.refresh_source_traffic_minute();", cancellationToken: cancellationToken));
    }
}
