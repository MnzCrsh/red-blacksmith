using System.Diagnostics;
using RedBlacksmith.TrafficProcessing.Models.Abstracts;
using RedBlacksmith.TrafficProcessing.Models.DomainModels;

namespace RedBlacksmith.TrafficProcessing.Application;

/// <inheritdoc/>
internal class PacketEventProcessor(IPacketEventStore eventStore, IPacketEventProjectionWriter writer) 
    : IPacketEventProcessor
{
    /// <inheritdoc/>
    public async Task ProcessBatchAsync(Guid batchId, IReadOnlyList<PacketEvent> events, CancellationToken cancellationToken)
    {
        var startedAt = Stopwatch.GetTimestamp();
        
        using var activity = TrafficProcessingTelemetry.StartBatchProcessObserving(batchId, events.Count);

        try
        {
            await StoreBatch(batchId, events, cancellationToken);
            TrafficProcessingTelemetry.RecordBatchProcessed(events.Count);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            activity?.SetTag("operation.cancelled", true);
            throw;
        }
        catch (Exception exception)
        {
            activity?.SetStatus(ActivityStatusCode.Error);
            activity?.SetTag("error.type", exception.GetType().FullName);

            TrafficProcessingTelemetry.RecordBatchFailed();

            throw;
        }
        finally
        {
            TrafficProcessingTelemetry.RecordBatchDuration(Stopwatch.GetElapsedTime(startedAt));
        }
    }

    private async Task StoreBatch(Guid batchId, IReadOnlyList<PacketEvent> events, CancellationToken cancellationToken)
    {
        Validate(batchId, events, cancellationToken);

        await eventStore.AppendBatchAsync(batchId, events, cancellationToken);
        await writer.ApplyBatchAsync(batchId, events, cancellationToken);
    }

    private static void Validate(Guid batchId, IReadOnlyList<PacketEvent> events, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        
        if (batchId == Guid.Empty)
            throw new ArgumentException("Идентификатор пакета не может быть пустым.", nameof(batchId));

        if (events.Count == 0)
            throw new ArgumentException("Пакет должен содержать хотя бы одно событие.", nameof(events));
    }
}
