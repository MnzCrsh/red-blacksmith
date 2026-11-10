using RedBlacksmith.TrafficProcessing.Application;
using RedBlacksmith.TrafficProcessing.Db;
using RedBlacksmith.TrafficProcessing.Kafka;
using RedBlacksmith.TrafficProcessing.Postgres;
using TrafficProcessing;

var builder = WebApplication.CreateBuilder(args);

var services = builder.Services;

services.AddTrafficProcessingObservability()
        .AddApplicationServices()
        .AddPostgresServices()
        .AddClickhouseServices(builder.Configuration)
        .AddKafkaServices();

var app = builder.Build();

app.MigratePostgreSql();
await app.MigrateClickhouseSql();
await app.RunAsync();
