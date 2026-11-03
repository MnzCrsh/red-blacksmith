using RedBlacksmith.TrafficProcessing.Models.DomainModels;

namespace RedBlacksmith.TrafficProcessing.Models.Abstracts;

/// <summary>
/// Обеспечивает хранение исходных сетевых событий.
/// </summary>
public interface IPacketEventStore
{
    /// <summary>
    /// Сохраняет пакет сетевых событий.
    /// Повторная запись того же пакета не должна создавать дубликаты.
    /// </summary>
    /// <param name="batchId">Уникальный идентификатор Kafka-пакета.</param>
    /// <param name="events">Сетевые события для сохранения.</param>
    /// <param name="cancellationToken">Токен отмены операции.</param>
    public Task AppendBatchAsync(Guid batchId, IReadOnlyList<PacketEvent> events, CancellationToken cancellationToken);
}
