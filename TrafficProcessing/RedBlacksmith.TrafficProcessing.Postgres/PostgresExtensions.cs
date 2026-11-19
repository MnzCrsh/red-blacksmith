using FluentMigrator.Runner;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using RedBlacksmith.TrafficProcessing.Models.Abstracts;

namespace RedBlacksmith.TrafficProcessing.Postgres;

public static class PostgresExtensions
{
    public static IServiceCollection AddPostgresServices(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration["Postgres:ConnectionString"]
                               ?? throw new InvalidOperationException("Не задан Postgres:ConnectionString.");
        AddMigrations(services, connectionString);
        
        services.AddSingleton<NpgsqlDataSource>(_ => NpgsqlDataSource.Create(connectionString)); 
        
        services.AddSingleton<IPacketEventProjectionStore, PostgresPacketEventProjectionStore>();
        services.AddSingleton<MaterializedViewRefreshService>();
        services.AddHostedService<MaterializedViewRefreshWorker>();
        return services;
    }

    private static void AddMigrations(IServiceCollection services, string connectionString) =>
        services.AddFluentMigratorCore()
                .ConfigureRunner(runner => runner.AddPostgres()
                                                 .WithGlobalConnectionString(connectionString)
                                                 .ScanIn(typeof(PostgresExtensions).Assembly)
                                                 .For
                                                 .Migrations());
}