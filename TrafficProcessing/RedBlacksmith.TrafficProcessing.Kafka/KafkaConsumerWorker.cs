using System.Diagnostics;
using Confluent.Kafka;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Polly;
using RedBlacksmith.TrafficProcessing.Kafka.RetryPolicies;
using RedBlacksmith.TrafficProcessing.Models.Abstracts;
using RedBlacksmith.TrafficProcessing.Models.DomainModels;

namespace RedBlacksmith.TrafficProcessing.Kafka;

/// <summary>
/// Worker-сервис читающий сообщения батчами из Kafka.
/// </summary>
internal sealed class KafkaConsumerWorker(
    IConfiguration configuration,
    IPacketEventProcessor eventProcessor,
    KafkaDeadLetterPublisher dlqPublisher,
    ILogger<KafkaConsumerWorker> logger,
    ConsumerConfig consumerConfig) 
    : BackgroundService
{
    /// <summary>
    /// Максимальное количество попыток обработки.
    /// </summary>
    private const int MaxRetryAttempts  = 9;
    
    /// <summary>
    /// Время между повторами по умолчанию.
    /// </summary>
    private const int DefaultRetryDelayMs = 250;
    
    /// <summary>
    /// Максимальный лимит на время между повторами.
    /// </summary>
    private const int RetryDelayMsThreshold     = 10_000;
    
    /// <summary>
    /// Время ожидания про ошибки потребления сообщения.
    /// </summary>
    private const int ConsumeErrorDelayMs = 250;
    
    
    private const string ProcessBatchOperation      = "process_batch";
    private const string PublishDeadLetterOperation = "publish_dlq";
    
    /// <summary>
    /// Конвеер retry стратегии.
    /// </summary>
    private readonly ResiliencePipeline _retryPipeline = RetryProvider.CreateRetryPipeline(
     logger, MaxRetryAttempts, DefaultRetryDelayMs, RetryDelayMsThreshold);
    
    /// <summary>
    /// Потребляет сообщения из Kafka с retry политиками и обработкой ошибок.
    /// </summary>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var consumer = CreateConsumer();
        SubscribeToTopic(consumer);

        try
        {
            await ConsumeMessage(consumer, stoppingToken);
        }
        finally
        {
            consumer.Close();
        }
    }

    private async Task ConsumeMessage(IConsumer<Ignore, byte[]> consumer, CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var consumeResult = await TryConsume(consumer, stoppingToken);
            
            if (consumeResult is null || consumeResult.IsPartitionEOF)
                continue;

            await HandleMessage(consumer, consumeResult, stoppingToken);
        }
    }

    private async Task<ConsumeResult<Ignore, byte[]>?> TryConsume(IConsumer<Ignore, byte[]> consumer,
        CancellationToken cancellationToken)
    {
        try
        {
            return consumer.Consume(cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return null;
        }
        catch (ConsumeException exception) when (!exception.Error.IsFatal)
        {
            KafkaConsumerTelemetry.RecordRecoverableConsumerError();

            logger.LogWarning(exception, "Recoverable Kafka consumer error");

            await Task.Delay(ConsumeErrorDelayMs, cancellationToken);

            return null;
        }
    }

    private async Task HandleMessage(IConsumer<Ignore, byte[]> consumer,
        ConsumeResult<Ignore, byte[]> consumeResult,
        CancellationToken cancellationToken)
    {
        using var activity = KafkaConsumerTelemetry.StartMessageProcessing(consumeResult, consumerConfig.GroupId);
        KafkaConsumerTelemetry.RecordMessageConsumed();
        try
        {
            await ProcessMessage(consumer, consumeResult, activity, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            KafkaConsumerTelemetry.RecordMessageFailed();
            KafkaConsumerTelemetry.RecordMessageError(activity, exception);

            throw;
        }
    }
    
    private async Task ProcessMessage(
        IConsumer<Ignore, byte[]> consumer,
        ConsumeResult<Ignore, byte[]> consumeResult,
        Activity? activity,
        CancellationToken cancellationToken)
    {
        var payload = consumeResult.Message.Value ?? [];

        (Guid BatchId, DateTimeOffset CreatedAtUtc, PacketEvent[] Events) batch;

        try
        {
            batch = KafkaBatchParser.Parse(payload);
        }
        catch (InvalidKafkaBatchException exception)
        {
            await PublishDeadLetter(consumer, consumeResult, activity, exception, cancellationToken);
            return;
        }

        await ProcessWithRetry(batch.Events, batch.BatchId, cancellationToken);

        CommitMessage(consumer, consumeResult);

        logger.LogDebug("Committed {TopicPartitionOffset} for batch {BatchId} containing {EventCount} events.",
                        consumeResult.TopicPartitionOffset, batch.BatchId, batch.Events.Length);
    }


    /// <summary>
    /// Обрабатывает сообщение из Kafka.
    /// </summary>
    /// <param name="batch">Kafka-батч для обработки.</param>
    /// <param name="batchId">Идентификатор kafka-батча.</param>
    /// <param name="stoppingToken">Токен остановки.</param>
    private async Task ProcessWithRetry(
        PacketEvent[] batch,
        Guid batchId,
        CancellationToken stoppingToken) =>
        await ExecuteWithRetryAsync(token => eventProcessor.ProcessBatchAsync(batchId, batch, token),
                                    "Processing packet batch",
                                    ProcessBatchOperation,
                                    batchId.ToString(),
                                    stoppingToken);

    /// <summary>
    /// Публикует сообщение в Dead Letter Queue.
    /// </summary>
    /// <param name="consumeResult">Результат считывания сообщения из Kafka.</param>
    /// <param name="exception">Возникшее исключение при считывании.</param>
    /// <param name="stoppingToken">Токен остановки.</param>
    private async Task PublishDeadLetter(
        IConsumer<Ignore, byte[]> consumer,
        ConsumeResult<Ignore, byte[]> consumeResult,
        Activity? activity,
        InvalidKafkaBatchException exception,
        CancellationToken stoppingToken)
    {
        KafkaConsumerTelemetry.RecordInvalidBatch();
        KafkaConsumerTelemetry.RecordMessageError(activity, exception);
        
        await ExecuteWithRetryAsync(token => dlqPublisher.PublishAsync(consumeResult, exception, token),
                                    "Publishing dead letter to DLQ",
                                    PublishDeadLetterOperation,
                                    consumeResult.TopicPartitionOffset.ToString(),
                                    stoppingToken);
        
        KafkaConsumerTelemetry.RecordDeadLetterPublished();
        
        CommitMessage(consumer, consumeResult);
    }
    
    private static void CommitMessage(IConsumer<Ignore, byte[]> consumer, ConsumeResult<Ignore, byte[]> consumeResult)
    {
        consumer.Commit(consumeResult);
        KafkaConsumerTelemetry.RecordMessageCommitted();
    }

    /// <summary>
    /// Оборачивает операцию в конвеер с Retry политикой.
    /// </summary>
    /// <param name="operation">Оборачиваемая операция.</param>
    /// <param name="operationName">Наименование оборачиваемой операции.</param>
    /// <param name="target">Обрабатываемый объект.</param>
    /// <param name="retryPipeline">Конвеер retry-политики.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    private async Task ExecuteWithRetryAsync(
        Func<CancellationToken, Task> operation,
        string operationName,
        string operationMetricName,
        string target,
        CancellationToken cancellationToken)
    {
        const int totalAllowedAttempts = MaxRetryAttempts + 1;

        var attempt = 0;

        try
        {
            await _retryPipeline.ExecuteAsync(ExecuteAttemptAsync, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            logger.LogCritical(exception,
                               "{Operation} failed for {Target} after {Attempts} attempts. The Kafka offset will not be committed.",
                               operationName, target, totalAllowedAttempts);

            throw;
        }

        return;

        ValueTask ExecuteAttemptAsync(CancellationToken token)
        {
            if (attempt > 0) 
                KafkaConsumerTelemetry.RecordRetry(operationMetricName);

            attempt++;

            return new ValueTask(operation(token));
        }
    }
    
    private IConsumer<Ignore, byte[]> CreateConsumer() =>
        new ConsumerBuilder<Ignore, byte[]>(consumerConfig)
            .SetKeyDeserializer(Deserializers.Ignore)
            .SetValueDeserializer(Deserializers.ByteArray)
            .SetErrorHandler((_, error) => logger.LogError("Kafka consumer error: {Reason}", error.Reason))
            .Build();
    
    private void SubscribeToTopic(IConsumer<Ignore, byte[]> consumer)
    {
        var topic = configuration["Kafka:Topic"];
        consumer.Subscribe(topic);
        logger.LogInformation("Kafka consumer started. Topic: {Topic}, group: {GroupId}.", topic, consumerConfig.GroupId);
    }
}
