using RedBlacksmith.TrafficProcessing.Models;

namespace RedBlacksmith.TrafficProcessing.Application;

public interface IPacketEventProcessor
{
    Task ProcessBatchAsync(IReadOnlyList<PacketEvent> events, CancellationToken cancellationToken);
}