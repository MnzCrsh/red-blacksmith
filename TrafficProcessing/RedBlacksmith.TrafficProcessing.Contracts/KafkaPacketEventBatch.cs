using System.Text.Json.Serialization;

namespace RedBlacksmith.TrafficProcessing.Models;

public sealed record KafkaPacketEventBatch
{
    [JsonPropertyName("batchId")]
    public Guid BatchId { get; init; }
    
    [JsonPropertyName("createdAtUtc")]
    public DateTimeOffset CreatedAtUtc { get; init; }
    
    [JsonPropertyName("items")]
    public KafkaPacketEvent[]? Items { get; init; }
}