using FluentMigrator.Runner;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace RedBlacksmith.TrafficProcessing.Postgres;

public static class PostgreSqlMigrationExtensions
{
    /// <summary>
    /// Применяет все неприменённые миграции PostgreSQL.
    /// </summary>
    public static IHost MigratePostgreSql(this IHost host)
    {
        using var scope = host.Services.CreateScope();
        
        var runner = scope.ServiceProvider.GetRequiredService<IMigrationRunner>();
        
        runner.MigrateUp();
        
        return host;
    }
}
