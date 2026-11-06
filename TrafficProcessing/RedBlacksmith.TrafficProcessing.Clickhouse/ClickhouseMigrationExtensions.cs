using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace RedBlacksmith.TrafficProcessing.Db;

public static class ClickhouseMigrationExtensions
{
    /// <summary>
    /// Применяет все неприменённые миграции Clickhouse.
    /// </summary>
    public static async Task MigrateClickhouseSql(this IHost host)
    {
        using var scope = host.Services.CreateScope();
        
        var runner = scope.ServiceProvider.GetRequiredService<ClickhouseMigrationRunner>();
        
        await runner.RunAsync();
    }  
}