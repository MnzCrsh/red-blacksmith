using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace RedBlacksmith.TrafficProcessing.Kafka;

internal sealed class KafkaConsumerReadinessHealthCheck(KafkaConsumerReadinessState readinessState) : IHealthCheck
{
    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!readinessState.IsWorkerStarted)
            return Task.FromResult(HealthCheckResult.Unhealthy("Kafka Consumer Worker не запущен."));

        var assignedPartitions = readinessState.AssignedPartitions;
        return Task.FromResult(assignedPartitions is 0
                                   ? HealthCheckResult.Unhealthy("Kafka Consumer не получил ни одной партиции.")
                                   : HealthCheckResult.Healthy($"Kafka Consumer готов. Назначено партиций: {assignedPartitions}."));
    } 
}
