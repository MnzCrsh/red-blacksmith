using System.Diagnostics;
using System.Diagnostics.Metrics;
// ReSharper disable ExplicitCallerInfoArgument

namespace RedBlacksmith.TrafficProcessing.Application;

/// <summary>
/// Предоставляет доступ к методам для снятия метрик и трассировки.
/// </summary>
public static class TrafficProcessingTelemetry
{
    /// <summary>
    /// Наименование источника телеметрии.
    /// </summary>
    public const string InstrumentationName = "RedBlacksmith.TrafficProcessing";
    
    /// <summary>
    /// Источник телеметрии.
    /// </summary>
    private static readonly ActivitySource ActivitySource = new(InstrumentationName);
    
    private static readonly Meter Meter = new(InstrumentationName);
    
    /// <summary>
    /// Счетчик сохраненных батчей с сообщениями в хранилище.
    /// </summary>
    private static readonly Counter<long> ProcessedBatches = Meter
        .CreateCounter<long>("traffic.processing.batches.processed", "{batch}");
    
    /// <summary>
    /// Счетчик сохраненных сетевых событий.
    /// </summary>
    private static readonly Counter<long> ProcessedEvents = Meter
        .CreateCounter<long>("traffic.processing.events.handled", "{event}");
    
    /// <summary>
    /// Счетчик необработанных батчей. 
    /// </summary>
    private static readonly Counter<long> FailedBatches = Meter
        .CreateCounter<long>("traffic.processing.batches.failed", "{batch}");
    
    /// <summary>
    /// Гистограмма длительности сохранения батча.
    /// </summary>
    private static readonly Histogram<double> BatchProcessingDuration = Meter
        .CreateHistogram<double>("traffic.processing.batch.duration", "ms");

    /// <summary>
    /// Запускает трассировку сохранения батчей.
    /// </summary>
    /// <param name="batchId">Идентификатор полученного батча.</param>
    /// <param name="eventCount">Колличество событий внутри батча.</param>
    public static Activity? StartBatchProcessObserving(Guid batchId, int eventCount)
    {
        var activity = ActivitySource.StartActivity("traffic.batch.process", ActivityKind.Consumer);

        activity?.SetTag("messaging.system", "kafka");
        activity?.SetTag("messaging.operation.type", "process");
        activity?.SetTag("traffic.batch.id", batchId.ToString());
        activity?.SetTag("traffic.batch.size", eventCount.ToString());
        
        return activity;
    }
    
    /// <summary>
    /// Регистрирует успешно сохраненный batch и количество событий в нём.
    /// </summary>
    public static void RecordBatchProcessed(int eventCount)
    {
        ProcessedBatches.Add(1);
        ProcessedEvents.Add(eventCount);
    }
    
    /// <summary>
    /// Регистрирует неудачную попытку обработки батча.
    /// </summary>
    public static void RecordBatchFailed() => FailedBatches.Add(1);

    /// <summary>
    /// Регистрирует продолжительность обработки батча.
    /// </summary>
    public static void RecordBatchDuration(TimeSpan duration) => BatchProcessingDuration.Record(duration.TotalMilliseconds);
}
