using System.Text.RegularExpressions;
using ClickHouse.Driver;
using ClickHouse.Driver.ADO;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using RedBlacksmith.TrafficProcessing.Db.Models;
using RedBlacksmith.TrafficProcessing.Models.Abstracts;

namespace RedBlacksmith.TrafficProcessing.Db;

public static class ClickhouseServicesExtensions
{
    private static readonly Regex DatabaseNameRegex = new("^[A-Za-z_][A-Za-z0-9_]*$", RegexOptions.Compiled | RegexOptions.CultureInvariant);
    
    public static IServiceCollection AddClickhouseServices(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = ValidateConfig(configuration, out var database);
        var options = new ClickHouseOptions(connectionString, database);
        services.AddSingleton(options);
        
        services.AddSingleton(_ => RegisterClickHouseClient(options));
        
        services.AddSingleton<IPacketEventStore, ClickhousePacketEventStore>()
                .AddSingleton<ClickhouseMigrationRunner>();
        
        return services;
    }

    private static ClickHouseClient RegisterClickHouseClient(ClickHouseOptions options)
    {
        var settings = new ClickHouseClientSettings(options.ConnectionString) { Database = "default" };

        var client = new ClickHouseClient(settings);
        client.RegisterBinaryInsertType<ClickHousePacketEventRow>();

        return client;
    }

    private static string ValidateConfig(IConfiguration configuration, out string database)
    {
        var section = configuration.GetSection("ClickHouse");
        
        var connectionString = section["ConnectionString"];
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new InvalidOperationException("Не задан параметр ClickHouse:ConnectionString.");
        
        database = section["Database"] ?? "traffic";
        
        return !DatabaseNameRegex.IsMatch(database)
            ? throw new InvalidOperationException("Некорректное имя базы данных ClickHouse.") 
            : connectionString;
    }
}