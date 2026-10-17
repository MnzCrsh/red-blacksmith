namespace RedBlacksmith.TrafficIngestion.Models;

internal sealed record KafkaPacketEvent(
    Guid EventId,
    Guid SourceId,
    DateTimeOffset OccurredAtUtc,
    string SourceIp,
    string DestinationIp,
    ushort? SourcePort,
    ushort? DestinationPort,
    byte Protocol,
    uint PacketLengthBytes);