using RedBlacksmith.TrafficProcessing.Models.DomainModels;

namespace RedBlacksmith.TrafficProcessing.Models.Abstracts;

/// <summary>
/// Обновляет аналитические проекции на основе сетевых событий.
/// </summary>
public interface IPacketEventProjectionWriter
{
    /// <summary>
    /// Применяет пакет событий к аналитическим проекциям.
    /// Повторное применение пакета не должно повторно учитывать его события.
    /// </summary>
    /// <param name="batchId">Уникальный идентификатор Kafka-пакета.</param>
    /// <param name="events">Сетевые события для обработки.</param>
    /// <param name="cancellationToken">Токен отмены операции.</param>
    Task ApplyBatchAsync(
        Guid batchId,
        IReadOnlyList<PacketEvent> events,
        CancellationToken cancellationToken);
}