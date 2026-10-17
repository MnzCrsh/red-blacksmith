namespace RedBlacksmith.TrafficIngestion.Models;

internal sealed record KafkaBatchPayload(
    Guid BatchId,
    byte[] Payload,
    int EventCount);