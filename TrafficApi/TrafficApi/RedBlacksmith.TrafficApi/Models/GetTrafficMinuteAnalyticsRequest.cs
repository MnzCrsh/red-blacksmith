namespace TrafficApi.Models;

public record GetTrafficMinuteAnalyticsRequest
{
    /// <summary>
    /// Начало временного диапазона в UTC.
    /// </summary>
    public DateTime FromUtc { get; set; }

    /// <summary>
    /// Конец временного диапазона в UTC.
    /// </summary>
    public DateTime ToUtc { get; set; }  
}
