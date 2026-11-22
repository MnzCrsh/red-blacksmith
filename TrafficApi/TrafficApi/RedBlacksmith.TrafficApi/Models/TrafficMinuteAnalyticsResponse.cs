namespace TrafficApi.Models;

public record TrafficMinuteAnalyticsResponse
{
    /// <summary>
    /// Начало минуты в UTC.
    /// </summary>
    public DateTime BucketStartUtc { get; init; }

    /// <summary>
    /// Общее количество пакетов.
    /// </summary>
    public long PacketCount { get; init; }

    /// <summary>
    /// Общий объём переданных данных в байтах.
    /// </summary>
    public long TotalBytes { get; init; }

    /// <summary>
    /// Суммарное количество уникальных назначений по отдельным источникам.
    /// </summary>
    public long DestinationCount { get; init; }
}