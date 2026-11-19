using Confluent.Kafka;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace RedBlacksmith.HealthChecks;

public class KafkaReadinessHealthCheck(IAdminClient adminClient, IConfiguration configuration) : IHealthCheck
{
    private static readonly TimeSpan MetadataTimeout = TimeSpan.FromSeconds(2);

    private readonly string _topic = configuration["Kafka:Topic"] ?? throw new InvalidOperationException("Не задан Kafka:Topic.");
    
    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = new())
    {
        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            var metadata = adminClient.GetMetadata(_topic, MetadataTimeout);

            if (metadata.Brokers.Count is 0)
            {
                return Task.FromResult(HealthCheckResult.Unhealthy("Kafka не вернула информацию о брокерах."));
            }

            var topic = metadata.Topics.FirstOrDefault(item => string.Equals(item.Topic, _topic, StringComparison.Ordinal));

            if (topic is null)
            {
                return Task.FromResult(HealthCheckResult.Unhealthy($"Топик Kafka '{_topic}' не найден."));
            }

            if (topic.Error.IsError)
            {
                return Task.FromResult(HealthCheckResult.Unhealthy($"Топик Kafka '{_topic}' недоступен: {topic.Error.Reason}."));
            }

            if (topic.Partitions.Count is 0)
            {
                return Task.FromResult(HealthCheckResult.Unhealthy($"Топик Kafka '{_topic}' не содержит партиций."));
            }

            var unavailablePartitions = topic.Partitions
                                             .Where(static partition => partition.Error.IsError || partition.Leader < 0)
                                             .Select(static partition => partition.PartitionId)
                                             .ToArray();

            return Task.FromResult(unavailablePartitions.Length > 0
                                       ? HealthCheckResult.Unhealthy($"У топика Kafka '{_topic}' отсутствуют доступные лидеры партиций: {string.Join(", ", unavailablePartitions)}.") 
                                       : HealthCheckResult.Healthy());
        }
        catch (KafkaException exception)
        {
            return Task.FromResult(HealthCheckResult.Unhealthy("Не удалось подключиться к Kafka.", exception));
        }
    }
}
