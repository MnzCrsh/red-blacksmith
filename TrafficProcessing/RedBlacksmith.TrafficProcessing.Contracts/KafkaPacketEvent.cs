using System.Text.Json.Serialization;

namespace RedBlacksmith.TrafficProcessing.Models;

public sealed record KafkaPacketEvent
{
    [JsonPropertyName("eventId")]
    public Guid EventId { get; init; }
    
    [JsonPropertyName("sourceId")]
    public Guid SourceId { get; init; }
    
    [JsonPropertyName("occurredAtUtc")]
    public DateTimeOffset OccurredAtUtc { get; init; }
    
    [JsonPropertyName("sourceIp")]
    public string? SourceIp { get; init; }
    
    [JsonPropertyName("destinationIp")]
    public string? DestinationIp { get; init; }
    
    [JsonPropertyName("sourcePort")]
    public ushort? SourcePort { get; init; }

    [JsonPropertyName("destinationPort")]
    public ushort? DestinationPort { get; init; }
    
    [JsonPropertyName("protocol")]
    public byte Protocol { get; init; }
    
    [JsonPropertyName("packetLengthBytes")]
    public uint PacketLengthBytes { get; init; }
}