using RedBlacksmith.TrafficProcessing.Models.DomainModels;

namespace RedBlacksmith.TrafficProcessing.Models.Abstracts;

public interface IPacketEventProcessor
{
    Task ProcessBatchAsync(Guid id, IReadOnlyList<PacketEvent> events, CancellationToken cancellationToken);
}