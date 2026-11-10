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
    /// Счетчик обработанных kafka-батчей.
    /// </summary>
    private static readonly Counter<long> ProcessedBatches = Meter
        .CreateCounter<long>("traffic.processing.batches.processed", "{batch}");
    
    /// <summary>
    /// Счетчик обработанных сетевых событий.
    /// </summary>
    private static readonly Counter<long> ProcessedEvents = Meter
        .CreateCounter<long>("traffic.processing.events.handled", "{event}");
    
    /// <summary>
    /// Счетчик необработанных Kafka-батчей. 
    /// </summary>
    private static readonly Counter<long> FailedBatches = Meter
        .CreateCounter<long>("traffic.processing.batches.failed", "{batch}");
    
    /// <summary>
    /// Счетчик повторных попыток обработки сообщения.
    /// </summary>
    private static readonly Counter<long> ConsumerRetries = Meter
        .CreateCounter<long>("traffic.processing.consumer.retries", "{retry}");
    
    /// <summary>
    /// Счетчик сообщений отправленных в Dead Letter Queue.
    /// </summary>
    private static readonly Counter<long> DeadLetterMessages = Meter
        .CreateCounter<long>("traffic.processing.dead_letter.messages", "{message}");
    
    /// <summary>
    /// Гистограмма длительности обработки kafka-батча.
    /// </summary>
    private static readonly Histogram<double> BatchProcessingDuration = Meter
        .CreateHistogram<double>("traffic.processing.batch.duration", "ms");

    /// <summary>
    /// Запускает трассировку обработки Kafka-батчей.
    /// </summary>
    /// <param name="batchId">Идентификатор kafka-батча.</param>
    /// <param name="eventCount">Колличество событий.</param>
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
    /// Регистрирует успешно обработанный batch и количество событий в нём.
    /// </summary>
    public static void RecordBatchProcessed(int eventCount)
    {
        ProcessedBatches.Add(1);
        ProcessedEvents.Add(eventCount);
    }
    
    /// <summary>
    /// Регистрирует неудачную попытку обработки batch.
    /// </summary>
    public static void RecordBatchFailed()
    {
        FailedBatches.Add(1);
    }
    
    /// <summary>
    /// Регистрирует повторную попытку обработки Kafka-сообщения.
    /// </summary>
    public static void RecordConsumerRetry()
    {
        ConsumerRetries.Add(1);
    }
    
    /// <summary>
    /// Регистрирует успешно опубликованное сообщение в DLQ.
    /// </summary>
    public static void RecordDeadLetterPublished()
    {
        DeadLetterMessages.Add(1);
    }
    
    /// <summary>
    /// Регистрирует продолжительность обработки batch.
    /// </summary>
    public static void RecordBatchDuration(TimeSpan duration)
    {
        BatchProcessingDuration.Record(duration.TotalMilliseconds);
    }
}
