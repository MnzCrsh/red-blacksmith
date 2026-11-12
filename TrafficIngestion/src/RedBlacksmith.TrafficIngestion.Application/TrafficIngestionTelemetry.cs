using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Globalization;

// ReSharper disable ExplicitCallerInfoArgument

namespace RedBlacksmith.TrafficIngestion.Application;

public static class TrafficIngestionTelemetry
{
    /// <summary>
    /// Имя источника телеметрии сервиса.
    /// </summary>
    public const string InstrumentationName = "RedBlacksmith.TrafficIngestion";
    
    private static readonly ActivitySource ActivitySource = new(InstrumentationName);

    private static readonly Meter Meter = new(InstrumentationName);

    private static readonly Counter<long> AcceptedEvents = Meter
        .CreateCounter<long>("traffic.ingestion.events.accepted", "{event}");

    private static readonly Counter<long> RejectedEvents = Meter
        .CreateCounter<long>("traffic.ingestion.events.rejected", "{event}");

    private static readonly Histogram<double> ChannelWriteDuration = Meter
        .CreateHistogram<double>("traffic.ingestion.channel.write.duration", "s");

    private static readonly Counter<long> PublishedBatches = Meter
        .CreateCounter<long>("traffic.ingestion.kafka.batches.published", "{batch}");

    private static readonly Counter<long> PublishedEvents = Meter
        .CreateCounter<long>("traffic.ingestion.kafka.events.published", "{event}");

    private static readonly Counter<long> PublishRetries = Meter
        .CreateCounter<long>("traffic.ingestion.kafka.publish.retries", "{retry}");

    private static readonly Counter<long> PublishFailures = Meter
        .CreateCounter<long>("traffic.ingestion.kafka.publish.failures", "{failure}");

    private static readonly Histogram<long> PublishBatchSize = Meter
        .CreateHistogram<long>("traffic.ingestion.kafka.batch.size", "{event}");

    private static readonly Histogram<double> PublishDuration = Meter
        .CreateHistogram<double>("traffic.ingestion.kafka.publish.duration", "s");
    
    /// <summary>
    /// Начинает трассировку общей операции публикации batch.
    /// </summary>
    public static Activity? StartBatchPublish(Guid batchId, int eventCount)
    {
        var activity = ActivitySource.StartActivity("traffic.batch.publish");
        if (activity is not { IsAllDataRequested: true }) return activity;
        
        activity.SetTag("traffic.batch.id", batchId);
        activity.SetTag("traffic.batch.event_count", eventCount);

        return activity;
    }
    
    /// <summary>
    /// Начинает трассировку отправки сообщения в Kafka.
    /// </summary>
    public static Activity? StartKafkaProduce(Guid batchId, int eventCount, string topic)
    {
        var activity = ActivitySource.StartActivity($"send {topic}", ActivityKind.Producer);

        if (activity is not { IsAllDataRequested: true })
            return activity;

        activity.SetTag("messaging.system.name", "kafka");
        activity.SetTag("messaging.destination.name", topic);
        activity.SetTag("messaging.operation.name", "send");
        activity.SetTag("messaging.operation.type", "send");
        activity.SetTag("traffic.batch.id", batchId);
        activity.SetTag("traffic.batch.event_count", eventCount);

        return activity;
    }
    
    /// <summary>
    /// Регистрирует принятые и отклонённые сетевые события.
    /// </summary>
    public static void RecordGrpcStream(int acceptedCount, int rejectedCount)
    {
        if (acceptedCount > 0) AcceptedEvents.Add(acceptedCount);
        if (rejectedCount > 0) RejectedEvents.Add(rejectedCount);

        Activity.Current?.SetTag("traffic.ingestion.events.accepted", acceptedCount);
        Activity.Current?.SetTag("traffic.ingestion.events.rejected", rejectedCount);
    }
    
    /// <summary>
    /// Регистрирует продолжительность записи события в Channel.
    /// </summary>
    public static void RecordChannelWriteDuration(TimeSpan duration) => ChannelWriteDuration.Record(duration.TotalSeconds);

    /// <summary>
    /// Регистрирует успешно опубликованный Kafka batch.
    /// </summary>
    public static void RecordBatchPublished(int eventCount)
    {
        PublishedBatches.Add(1);
        PublishedEvents.Add(eventCount);
        PublishBatchSize.Record(eventCount);
    }
    
    /// <summary>
    /// Регистрирует продолжительность публикации batch.
    /// </summary>
    public static void RecordPublishDuration(TimeSpan duration) => PublishDuration.Record(duration.TotalSeconds);
    
    /// <summary>
    /// Регистрирует повторную попытку публикации.
    /// </summary>
    public static void RecordPublishRetry() => PublishRetries.Add(1);

    /// <summary>
    /// Регистрирует ошибку публикации.
    /// </summary>
    public static void RecordPublishFailure() => PublishFailures.Add(1);

    /// <summary>
    /// Отмечает ошибку публикации в трассировке.
    /// </summary>
    public static void RecordPublishError(Activity? activity, Exception exception)
    {
        if (activity is null)
            return;

        activity.SetStatus(ActivityStatusCode.Error);

        if (activity.IsAllDataRequested) activity.SetTag("error.type", exception.GetType().FullName);
    }
    
    /// <summary>
    /// Отмечает ошибку Kafka Producer в трассировке.
    /// </summary>
    public static void RecordKafkaProduceError(
        Activity? activity,
        Exception exception)
    {
        if (activity is null)
            return;

        activity.SetStatus(ActivityStatusCode.Error);

        if (activity.IsAllDataRequested)
            activity.SetTag("error.type", exception.GetType().FullName);
    }
    
    /// <summary>
    /// Регистрирует результат успешной отправки Kafka batch.
    /// </summary>
    public static void RecordKafkaProduceSuccess(Activity? activity, int partition, long offset)
    {
        if (activity is not { IsAllDataRequested: true })
            return;

        activity.SetTag("messaging.destination.partition.id", partition.ToString(CultureInfo.InvariantCulture));
        activity.SetTag("kafka.offset", offset);
    }
}
