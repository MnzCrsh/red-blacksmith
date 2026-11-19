namespace TrafficApi.Models;

public record GetSourceTrafficMinuteRequest
{
    /// <summary>
    /// Идентификатор источника трафика.
    /// </summary>
    public Guid SourceId { get; set; }

    /// <summary>
    /// Начало периода в UTC.
    /// </summary>
    public DateTimeOffset FromUtc { get; set; }

    /// <summary>
    /// Конец периода в UTC, не включая указанную границу.
    /// </summary>
    public DateTimeOffset ToUtc { get; set; }
}