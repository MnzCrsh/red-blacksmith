using RedBlacksmith.TrafficProcessing.Models.DomainModels;

namespace RedBlacksmith.TrafficProcessing.Models.Abstracts;

/// <summary>
/// Применяет события к аналитическим проекциям.
/// </summary>
public interface IPacketEventProjectionStore
{
    /// <summary>
    /// Атомарно применяет события к проекциям с защитой от повторной обработки.
    /// </summary>
    Task ApplyAsync(IReadOnlyCollection<PacketEvent> events, CancellationToken cancellationToken);
}