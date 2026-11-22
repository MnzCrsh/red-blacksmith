using FastEndpoints;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Npgsql;
using TrafficApi;

var builder = WebApplication.CreateBuilder(args);

var services = builder.Services;

var connectionString = builder.Configuration["Postgres:ConnectionString"]
                       ?? throw new InvalidOperationException("Не задан Postgres:ConnectionString.");

services.AddSingleton<NpgsqlDataSource>(_ => NpgsqlDataSource.Create(connectionString))
        // .AddAuthorization()
        .AddFastEndpoints();

builder.Services
       .AddHealthChecks()
       .AddCheck<PostgresReadinessHealthCheck>("postgres", tags: ["ready"]);

var app = builder.Build();

// app.UseRouting();
//
// app.UseAuthorization();

app.UseFastEndpoints();

app.MapHealthChecks("/health/live", new HealthCheckOptions
{
    Predicate = _ => false
});

app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("ready")
});

app.Run();
