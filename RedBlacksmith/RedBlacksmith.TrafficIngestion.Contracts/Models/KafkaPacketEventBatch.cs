namespace RedBlacksmith.TrafficIngestion.Models;

internal sealed record KafkaPacketEventBatch(
    Guid BatchId,
    DateTimeOffset CreatedAtUtc,
    KafkaPacketEvent[] Items);