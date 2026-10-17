using System.Threading.Channels;
using Confluent.Kafka;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RedBlacksmith.TrafficIngestion.Models;

namespace RedBlacksmith.TrafficIngestion.Application;

internal sealed class KafkaPublishWorker(
    ChannelReader<PacketEvent> channelReader,
    KafkaEventProducer kafkaProducer,
    IConfiguration config,
    ILogger<KafkaPublishWorker> logger) : BackgroundService
{
    private const int DefaultBatchSize       = 500; 
    private const int DefaultFlushIntervalMs = 25;
    private const int RetryDefaultMs         = 250; 
    private const int RetryMaxDelayMs        = 10_000;
    
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var batchSize = config.GetValue("Kafka:BatchSize", DefaultBatchSize);
        if (batchSize <= 0)
        {
            throw new InvalidOperationException("Kafka:BatchSize должен быть больше нуля.");
        }
        
        var flushIntervalMs = config.GetValue("Kafka:FlushIntervalMs", DefaultFlushIntervalMs);
        if (flushIntervalMs <= 0)
        {
            throw new InvalidOperationException("Kafka:FlushIntervalMs должен быть больше нуля.");
        }

        logger.LogInformation("Kafka Publish Worker started. Batch size: {BatchSize}, flush interval: {FlushIntervalMs} ms.",
                              batchSize,
                              flushIntervalMs);

        while (!stoppingToken.IsCancellationRequested)
        {
            List<PacketEvent> batch;

            try
            {
                batch = await ReadBatchAsync(batchSize, flushIntervalMs, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }

            if (batch.Count == 0)
            {
                break;
            }
            
            await PublishWithRetryAsync(batch, stoppingToken);
        }
    }

    private async Task<List<PacketEvent>> ReadBatchAsync(int batchSize, int flushIntervalMs,
        CancellationToken stoppingToken)
    {
        var batch = new List<PacketEvent>(batchSize);

        try
        {
            batch.Add(await channelReader.ReadAsync(stoppingToken));
        }
        catch (ChannelClosedException)
        {
            return batch;
        }
        
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);

        timeoutCts.CancelAfter(flushIntervalMs);
        
        try
        {
            while (batch.Count < batchSize)
            {
                while (batch.Count < batchSize && channelReader.TryRead(out PacketEvent? packetEvent))
                {
                    batch.Add(packetEvent);
                }

                if (batch.Count >= batchSize)
                {
                    break;
                }

                if (!await channelReader.WaitToReadAsync(timeoutCts.Token))
                {
                    break;
                }
            }
        }
        catch (OperationCanceledException)
            when(timeoutCts.IsCancellationRequested && !stoppingToken.IsCancellationRequested)
        {
            
        }

        return batch;
    }

    private async Task PublishWithRetryAsync(IReadOnlyList<PacketEvent> batch, CancellationToken stoppingToken)
    {
        var payload = KafkaEventProducer.PrepareBatch(batch);
        var retryDelayMs = RetryDefaultMs;

        
        while (true)
        {
            try
            {
                await kafkaProducer.ProduceBatchAsync(payload, stoppingToken);
                return;
            }
            catch (ProduceException<Null, byte[]> e) when (e.Error.IsFatal || e.Error.IsLocalError)
            {
                logger.LogWarning(e, "Kafka producer failed to publish batch with an ID: {BatchId}. Retry in {DelayMs} ms",
                                  payload.BatchId, RetryDefaultMs);
                
                await Task.Delay(RetryDefaultMs, stoppingToken);
                
                retryDelayMs = Math.Min(retryDelayMs * 2, RetryMaxDelayMs);
            }
        }
    }
}
