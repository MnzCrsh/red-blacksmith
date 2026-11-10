using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Globalization;
using System.Text;
using Confluent.Kafka;
using OpenTelemetry.Context.Propagation;

namespace RedBlacksmith.TrafficProcessing.Kafka;

public static class KafkaConsumerTelemetry
{
    /// <summary>
    /// Имя источника телеметрии Kafka Consumer.
    /// </summary>
    public const string InstrumentationName = "RedBlacksmith.TrafficProcessing.Kafka";
    
    private static readonly ActivitySource ActivitySource = new(InstrumentationName);

    private static readonly Meter Meter = new(InstrumentationName);

    private static readonly Counter<long> ConsumedMessages = Meter
        .CreateCounter<long>("traffic.processing.kafka.messages.consumed", "{message}");

    private static readonly Counter<long> CommittedMessages = Meter
        .CreateCounter<long>("traffic.processing.kafka.messages.committed", "{message}");

    private static readonly Counter<long> FailedMessages = Meter
        .CreateCounter<long>("traffic.processing.kafka.messages.failed", "{message}");

    private static readonly Counter<long> InvalidBatches = Meter
        .CreateCounter<long>("traffic.processing.kafka.batches.invalid", "{batch}");

    private static readonly Counter<long> RetryAttempts = Meter
        .CreateCounter<long>("traffic.processing.kafka.retry.attempts", "{attempt}");

    private static readonly Counter<long> PublishedDeadLetters = Meter
        .CreateCounter<long>("traffic.processing.kafka.dead_letter.published", "{message}");

    private static readonly Counter<long> RecoverableConsumerErrors = Meter
        .CreateCounter<long>("traffic.processing.kafka.consumer.errors", "{error}");

    /// <summary>
    /// Запускает трассировку обработки Kafka-сообщения.
    /// </summary>
    internal static Activity? StartMessageProcessing(ConsumeResult<Ignore, byte[]> consumeResult, string? consumerGroup)
    {
        var propagationContext = Propagators.DefaultTextMapPropagator
                                            .Extract(default, consumeResult.Message.Headers, GetHeaderValues);

        var activity = ActivitySource.StartActivity("kafka.process", ActivityKind.Consumer, propagationContext.ActivityContext);

        if (activity is not { IsAllDataRequested: true })
        {
            return activity;
        }

        SetTags(consumeResult, consumerGroup, activity);

        return activity;
    }

    private static void SetTags(ConsumeResult<Ignore, byte[]> consumeResult, string? consumerGroup, Activity activity)
    {
        activity.SetTag("messaging.system.name", "kafka");
        activity.SetTag("messaging.operation.name", "process");
        activity.SetTag("messaging.operation.type", "process");
        activity.SetTag("messaging.destination.name", consumeResult.Topic);
        
        activity.SetTag(
                        "messaging.destination.partition.id",
                        consumeResult.Partition.Value.ToString(CultureInfo.InvariantCulture));
        
        activity.SetTag("kafka.offset", consumeResult.Offset.Value.ToString());
        
        activity.SetTag(
                        "messaging.message.id",
                        $"{consumeResult.Topic}:{consumeResult.Partition.Value}:{consumeResult.Offset.Value}");

        if (!string.IsNullOrWhiteSpace(consumerGroup))
        {
            activity.SetTag("messaging.consumer.group.name", consumerGroup);
        }
    }

    /// <summary>
    /// Регистрирует получение Kafka-сообщения.
    /// </summary>
    internal static void RecordMessageConsumed() => ConsumedMessages.Add(1);

    /// <summary>
    /// Регистрирует успешный commit Kafka offset.
    /// </summary>
    internal static void RecordMessageCommitted() => CommittedMessages.Add(1);

    /// <summary>
    /// Регистрирует сообщение, обработка которого завершилась ошибкой.
    /// </summary>
    internal static void RecordMessageFailed() => FailedMessages.Add(1);

    /// <summary>
    /// Регистрирует некорректный Kafka батч.
    /// </summary>
    internal static void RecordInvalidBatch() => InvalidBatches.Add(1);

    /// <summary>
    /// Регистрирует повторную попытку выполнения операции.
    /// </summary>
    internal static void RecordRetry(string operation) => RetryAttempts
        .Add(1, new KeyValuePair<string, object?>("operation", operation));

    /// <summary>
    /// Регистрирует успешную публикацию сообщения в DLQ.
    /// </summary>
    internal static void RecordDeadLetterPublished() => PublishedDeadLetters.Add(1);

    /// <summary>
    /// Регистрирует восстанавливаемую ошибку Kafka Consumer.
    /// </summary>
    internal static void RecordRecoverableConsumerError() => RecoverableConsumerErrors.Add(1);

    /// <summary>
    /// Отмечает ошибку обработки Kafka-сообщения в трассировке.
    /// </summary>
    internal static void RecordMessageError(Activity? activity, Exception exception)
    {
        if (activity is null)
        {
            return;
        }

        activity.SetStatus(ActivityStatusCode.Error);

        if (activity.IsAllDataRequested)
        {
            activity.SetTag("error.type", exception.GetType().FullName);
        }
    }

    private static IEnumerable<string> GetHeaderValues(Headers? headers, string key)
    {
        if (headers is null)
            yield break;

        foreach (var header in headers)
        {
            if (!string.Equals(header.Key, key, StringComparison.OrdinalIgnoreCase))
                continue;

            var value = header.GetValueBytes();

            if (value is not null)
                yield return Encoding.UTF8.GetString(value);
        }
    }
}
