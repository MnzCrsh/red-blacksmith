using System.Text.Json;
using Confluent.Kafka;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using RedBlacksmith.TrafficIngestion.Models;

namespace RedBlacksmith.TrafficIngestion.Application;

/// <summary>
/// Kafka-продюсер.
/// </summary>
internal sealed class KafkaEventProducer(
    IConfiguration configuration,
    ILogger<KafkaEventProducer> logger,
    IProducer<Null, byte[]> producer)
    : IDisposable
{
    /// <summary>
    /// Параметры сериализации.
    /// </summary>
    private static readonly JsonSerializerOptions       JsonOptions = new(JsonSerializerDefaults.Web);
    
    /// <summary>
    /// Основной Kafka топик для сообщений.
    /// </summary>
    private readonly        string                      _topic = configuration["Kafka:Topic"]
                                                                 ?? throw new InvalidOperationException("Не задан конфиг Kafka:Topic.");
    
    public static KafkaBatchPayload PrepareBatch(IReadOnlyList<PacketEvent> batch)
    {
        var batchId = Guid.CreateVersion7();
        var items = new KafkaPacketEvent[batch.Count];

        for (var i = 0; i < batch.Count; i++)
        {
            items[i] = CreateMessage(batch[i]);
        }
        
        var message = new KafkaPacketEventBatch(batchId, DateTimeOffset.UtcNow, items);
        var payload = JsonSerializer.SerializeToUtf8Bytes(message, JsonOptions);
        
        return new KafkaBatchPayload(batchId, payload, items.Length);
    }

    /// <summary>
    /// Отправить батч с сообщениями в топик.
    /// </summary>
    /// <param name="batch">Batch содержащий модели события сетевого пакета</param>
    /// <param name="ct">Токен отмены.</param>
    public async Task ProduceBatchAsync(KafkaBatchPayload batch, CancellationToken ct) => 
        await producer.ProduceAsync(_topic, new Message<Null, byte[]> { Value = batch.Payload }, ct);

    /// <inheritdoc/>
    public void Dispose()
    {
        var remaining = producer.Flush(TimeSpan.FromSeconds(10));

        if (remaining > 0)
        {
            logger.LogWarning("Kafka producer flush timed out on shutdown. Unsent messages count: {Remaining}", remaining);
        }
        
        producer.Dispose();
    }
    
    private static KafkaPacketEvent CreateMessage(PacketEvent packetEvent) =>
        new(packetEvent.EventId,
            packetEvent.SourceId,
            packetEvent.OccurredAtUtc.ToUniversalTime(),
            packetEvent.SourceIp.ToString(),
            packetEvent.DestinationIp.ToString(),
            packetEvent.SourcePort,
            packetEvent.DestinationPort,
            packetEvent.Protocol,
            packetEvent.PacketLengthBytes);
}
