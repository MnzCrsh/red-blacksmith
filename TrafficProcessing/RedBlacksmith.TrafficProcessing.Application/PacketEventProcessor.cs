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
        Validate(batchId, events, cancellationToken);

        await eventStore.AppendBatchAsync(batchId, events, cancellationToken);
        await writer.ApplyBatchAsync(batchId, events, cancellationToken);
    }

    private static void Validate(Guid batchId, IReadOnlyList<PacketEvent> events, CancellationToken cancellationToken)
    {
        if (batchId == Guid.Empty)
            throw new ArgumentException("Идентификатор пакета не может быть пустым.", nameof(batchId));

        if (events.Count == 0)
            throw new ArgumentException("Пакет должен содержать хотя бы одно событие.", nameof(events));
        
        cancellationToken.ThrowIfCancellationRequested();
    }
}
