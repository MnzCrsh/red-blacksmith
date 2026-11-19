using FastEndpoints;
using Npgsql;

var builder = WebApplication.CreateBuilder(args);

var services = builder.Services;

var connectionString = builder.Configuration["Postgres:ConnectionString"]
                       ?? throw new InvalidOperationException("Не задан Postgres:ConnectionString.");

services.AddSingleton<NpgsqlDataSource>(_ => NpgsqlDataSource.Create(connectionString))
        .AddFastEndpoints();

var app = builder.Build();

app.UseFastEndpoints();

app.Run();
