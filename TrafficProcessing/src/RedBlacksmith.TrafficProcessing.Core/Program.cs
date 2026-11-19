using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using RedBlacksmith.TrafficProcessing.Application;
using RedBlacksmith.TrafficProcessing.Db;
using RedBlacksmith.TrafficProcessing.Kafka;
using RedBlacksmith.TrafficProcessing.Postgres;
using TrafficProcessing;

var builder = WebApplication.CreateBuilder(args);

builder.WebHost.ConfigureKestrel(options =>
{ 
        options.ListenAnyIP(8081, listenOptions => listenOptions.Protocols = HttpProtocols.Http1);
});


var services = builder.Services;

services.AddObservability()
        .AddApplicationServices()
        .AddPostgresServices(builder.Configuration)
        .AddClickhouseServices(builder.Configuration)
        .AddKafkaServices()
        .AddKafkaHealthChecks();

var app = builder.Build();

app.MigratePostgreSql();
await app.MigrateClickhouseSql();

app.MapHealthChecks("/health/live", new HealthCheckOptions
{
        Predicate = _ => false
});

app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
        Predicate = check => check.Tags.Contains("ready")
});

await app.RunAsync();
