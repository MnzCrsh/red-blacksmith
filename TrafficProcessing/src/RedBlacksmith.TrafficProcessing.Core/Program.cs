using RedBlacksmith.TrafficProcessing.Application;
using RedBlacksmith.TrafficProcessing.Db;
using RedBlacksmith.TrafficProcessing.Kafka;

var builder = WebApplication.CreateBuilder(args);

var services = builder.Services;

services.AddApplicationServices()
        .AddClickhouseServices(builder.Configuration)
        .AddKafkaServices();

var app = builder.Build();

app.Run();

