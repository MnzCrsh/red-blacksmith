using Microsoft.Extensions.Diagnostics.HealthChecks;
using Npgsql;

namespace TrafficApi;

public class PostgresReadinessHealthCheck (NpgsqlDataSource dataSource) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = new CancellationToken())
    {
        try
        {
            await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
            await using var command = new NpgsqlCommand("SELECT 1 FROM traffic_read.source_traffic_minute LIMIT 0;", connection);
            
            command.CommandTimeout = 3;

            await using var reader = await command.ExecuteReaderAsync(cancellationToken);

            return HealthCheckResult.Healthy();
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            return HealthCheckResult.Unhealthy("PostgreSQL или проекция недоступны.", exception);
        }
    
    }
}