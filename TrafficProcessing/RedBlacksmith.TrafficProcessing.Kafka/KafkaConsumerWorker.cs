using Confluent.Kafka;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Polly;
using RedBlacksmith.TrafficProcessing.Application;
using RedBlacksmith.TrafficProcessing.Kafka.RetryPolicies;
using RedBlacksmith.TrafficProcessing.Models;

namespace RedBlacksmith.TrafficProcessing.Kafka;

internal sealed class KafkaConsumerWorker(
    IConfiguration configuration,
    IPacketEventProcessor eventProcessor,
    KafkaDeadLetterPublisher dlqPublisher,
    ILogger<KafkaConsumerWorker> logger,
    ConsumerConfig consumerConfig) 
    : BackgroundService
{
    private const int DefaultMaxAttempts  = 10;
    
    private const int DefaultRetryDelayMs = 250;
    
    private const int MaxRetryDelayMs     = 10_000;
    
    private const int ConsumeErrorDelayMs = 250;
    
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var consumer = CreateConsumer();

        var retryPipeline = RetryProvider.CreateRetryPipeline(logger, DefaultMaxAttempts - 1, DefaultRetryDelayMs, MaxRetryDelayMs);
        
        SubscribeToTopic(consumer);

        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                ConsumeResult<Ignore, byte[]> consumeResult;
                try
                {
                    consumeResult = consumer.Consume(stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                catch (ConsumeException exception) when (!exception.Error.IsFatal)
                {
                    logger.LogWarning(exception, "Recoverable Kafka consumer error");
                    await Task.Delay(ConsumeErrorDelayMs, stoppingToken);

                    continue;
                }

                
                if (consumeResult.IsPartitionEOF) continue;

                
                var payload = consumeResult.Message.Value ?? [];
                (Guid BatchId, DateTimeOffset CreatedAtUtc, PacketEvent[] Events) batch;
                try
                {
                    batch = KafkaBatchParser.Parse(payload);
                }
                catch (InvalidKafkaBatchException exception)
                {
                    await PublishDeadLetterWithRetryAsync(dlqPublisher,
                                                          consumeResult,
                                                          exception,
                                                          retryPipeline,
                                                          stoppingToken);

                    consumer.Commit(consumeResult);
                    continue;
                }

                await ProcessWithRetryAsync(eventProcessor, batch.Events, batch.BatchId, retryPipeline, stoppingToken);
                consumer.Commit(consumeResult);

                logger.LogDebug("Committed {TopicPartitionOffset} for batch {BatchId} containing {EventCount} events.",
                                consumeResult.TopicPartitionOffset, batch.BatchId, batch.Events.Length);
            }
        }
        finally
        {
            consumer.Close();
        }
    }

    private void SubscribeToTopic(IConsumer<Ignore, byte[]> consumer)
    {
        var topic = configuration["Kafka:Topic"];
        consumer.Subscribe(topic);
        logger.LogInformation("Kafka consumer started. Topic: {Topic}, group: {GroupId}.", topic, consumerConfig.GroupId);
    }

    private IConsumer<Ignore, byte[]> CreateConsumer() =>
        new ConsumerBuilder<Ignore, byte[]>(consumerConfig)
            .SetKeyDeserializer(Deserializers.Ignore)
            .SetValueDeserializer(Deserializers.ByteArray)
            .SetErrorHandler((_, error) => logger.LogError("Kafka consumer error: {Reason}", error.Reason))
            .Build();

    private async Task ExecuteWithRetryAsync(
        Func<CancellationToken, Task> operation,
        string operationName,
        string target,
        ResiliencePipeline retryPipeline,
        CancellationToken cancellationToken)
    {
        try
        {
            await retryPipeline.ExecuteAsync(static async (op, token) => await op(token), operation,cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            logger.LogCritical(exception,
                               "{Operation} failed for {Target} after {Attempts} attempts. The Kafka offset will not be committed.",
                               operationName, target, DefaultMaxAttempts);

            throw;
        }
    }
    
    private async Task ProcessWithRetryAsync(IPacketEventProcessor packetEventProcessor,
        PacketEvent[] batch,
        Guid batchId,
        ResiliencePipeline retryPipeline,
        CancellationToken stoppingToken) =>
        await ExecuteWithRetryAsync(token => packetEventProcessor.ProcessBatchAsync(batch, token),
                                    "Processing packet batch",
                                    batchId.ToString(),
                                    retryPipeline,
                                    stoppingToken);

    private async Task PublishDeadLetterWithRetryAsync(KafkaDeadLetterPublisher deadLetterPublisher,
        ConsumeResult<Ignore, byte[]> consumeResult,
        Exception exception,
        ResiliencePipeline retryPipeline,
        CancellationToken stoppingToken) =>
        await ExecuteWithRetryAsync(token => deadLetterPublisher.PublishAsync(consumeResult, exception, token),
                                    "Publishing dead letter to DLQ",
                                    consumeResult.TopicPartitionOffset.ToString(),
                                    retryPipeline,
                                    stoppingToken);
}
