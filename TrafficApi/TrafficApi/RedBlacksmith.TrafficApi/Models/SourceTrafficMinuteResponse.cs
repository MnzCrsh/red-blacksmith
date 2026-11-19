namespace TrafficApi.Models;

/// <summary>
/// Минутная статистика сетевого трафика источника.
/// </summary>
public record SourceTrafficMinuteResponse(
    DateTimeOffset BucketStartUtc,
    Guid SourceId,
    long PacketCount,
    long TotalBytes,
    long UniqueDestinationCount);