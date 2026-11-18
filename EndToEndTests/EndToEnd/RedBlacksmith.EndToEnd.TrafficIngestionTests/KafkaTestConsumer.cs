using System.Text.Json;
using Confluent.Kafka;

namespace RedBlacksmith.EndToEnd.TrafficIngestionTests;

public class KafkaTestConsumer : IDisposable
{
    private readonly IConsumer<Ignore, byte[]> _consumer;
    
    /// <summary>
    /// Создаёт consumer с уникальной группой, читающий сообщения с начала топика.
    /// </summary>
    public KafkaTestConsumer(string bootstrapServers, string topic)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(bootstrapServers);
        ArgumentException.ThrowIfNullOrWhiteSpace(topic);

        var config = new ConsumerConfig
        {
            BootstrapServers = bootstrapServers,
            GroupId = $"end-to-end-tests-{Guid.NewGuid():N}",
            AutoOffsetReset = AutoOffsetReset.Earliest,
            EnableAutoCommit = false
        };

        _consumer = new ConsumerBuilder<Ignore, byte[]>(config).Build();
        _consumer.Subscribe(topic);
    }
    
    /// <summary>
    /// Ожидает публикации батча, содержащего указанное событие.
    /// </summary>
    public async Task<JsonDocument> ConsumeUntilEventAsync(Guid expectedEventId, CancellationToken cancellationToken)
    {
        while (true)
        {
            var result = await Task.Run(() => _consumer.Consume(cancellationToken), cancellationToken);

            var document = JsonDocument.Parse(result.Message.Value);

            var containsEvent = document.RootElement
                                        .GetProperty("items")
                                        .EnumerateArray()
                                        .Any(item => item.GetProperty("eventId").GetGuid() == expectedEventId);

            if (containsEvent)
            {
                return document;
            }

            document.Dispose();
        }
    }

    public void Dispose()
    {
        _consumer.Close();
        _consumer.Dispose();
    }
}
