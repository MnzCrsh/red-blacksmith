using System.Net;

namespace RedBlacksmith.TrafficProcessing.Models;

public record PacketEvent(
    Guid EventId,
    Guid SourceId,
    DateTimeOffset OccurredAtUtc,
    IPAddress SourceIp,
    IPAddress DestinationIp,
    ushort? SourcePort,
    ushort? DestinationPort,
    byte Protocol,
    uint PacketLengthBytes);
    