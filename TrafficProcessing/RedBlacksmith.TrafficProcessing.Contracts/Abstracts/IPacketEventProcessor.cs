using RedBlacksmith.TrafficProcessing.Models.DomainModels;

namespace RedBlacksmith.TrafficProcessing.Models.Abstracts;

/// <summary>
/// Обрабатывает пакеты сетевых событий, полученные из Kafka.
/// </summary>
public interface IPacketEventProcessor
{
    /// <summary>
    /// Сохраняет пакет событий и обновляет аналитические проекции.
    /// </summary>
    /// <param name="batchId">Идентификатор Kafka-батча.</param>
    /// <param name="events">Сетевые события пакета.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    Task ProcessBatchAsync(Guid batchId, IReadOnlyList<PacketEvent> events, CancellationToken cancellationToken);
}