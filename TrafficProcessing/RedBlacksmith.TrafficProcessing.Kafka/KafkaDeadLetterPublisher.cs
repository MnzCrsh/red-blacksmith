using System.Text;
using Confluent.Kafka;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;

namespace RedBlacksmith.TrafficProcessing.Kafka;

internal class KafkaDeadLetterPublisher(
    IProducer<Null, byte[]> producer,
    IConfiguration configuration,
    ILogger<KafkaDeadLetterPublisher> logger)
    : IDisposable
{
    private readonly string _topic = configuration["Kafka:DlqTopic"] ??
                                    throw new InvalidOperationException("Не задан конфиг Kafka:Topic.");

    public async Task PublishAsync(ConsumeResult<Ignore, byte[]> sourceRecord, Exception e, CancellationToken ct)
    {
        var headers = CreateKafkaHeaders(sourceRecord, e);

        var result = await producer.ProduceAsync(_topic, new Message<Null, byte[]>
        {
            Value = sourceRecord.Message.Value,
            Headers = headers
        }, ct);

        logger.LogWarning("Invalid message forwarded to DLQ at {TopicPartitionOffset}. Source: {SourceTopicPartitionOffset}",
                          result.TopicPartitionOffset,
                          sourceRecord.TopicPartitionOffset);
    }

    private static Headers CreateKafkaHeaders(ConsumeResult<Ignore, byte[]> sourceRecord, Exception e) =>
    [

        new("x-error-type", Encoding.UTF8.GetBytes(
                                                   e.GetType().FullName ?? e.GetType().Name)),

        new("x-error-message", Encoding.UTF8.GetBytes(
                                                      e.Message)),

        new("x-source-topic", Encoding.UTF8.GetBytes(
                                                     sourceRecord.Topic)),

        new("x-source-partition", Encoding.UTF8.GetBytes(
                                                         sourceRecord.Partition.Value.ToString())),

        new("x-source-offset", Encoding.UTF8.GetBytes(
                                                      sourceRecord.Offset.Value.ToString()))
    ];

    public void Dispose()
    {
        var remaining = producer.Flush(TimeSpan.FromSeconds(10));
        if (remaining > 0)
        {
            logger.LogWarning("DLQ Producer shutdown timed out. Pending Messages: {Count}.", remaining);
        }
        
        producer.Dispose();
    }
}