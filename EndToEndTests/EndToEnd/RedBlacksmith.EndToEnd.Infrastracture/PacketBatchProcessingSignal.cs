using System.Collections.Concurrent;

namespace RedBlacksmith.EndToEnd;

public class PacketBatchProcessingSignal
{
    private readonly ConcurrentDictionary<Guid, TaskCompletionSource<PacketBatchProcessingResult>> _signals = new();

    /// <summary>
    /// Ожидает завершения обработки конкретного batch.
    /// </summary>
    /// <param name="batchId">Идентификатор batch.</param>
    /// <param name="timeout">Максимальное время ожидания.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Результат обработки batch.</returns>
    public async Task<PacketBatchProcessingResult> WaitAsync(Guid batchId, TimeSpan timeout, CancellationToken cancellationToken = default)
    {
        var signal = GetSignal(batchId);
        try
        {
            return await signal.Task.WaitAsync(timeout, cancellationToken);
        }
        finally
        {
            _signals.TryRemove(batchId, out _);
        }
    }

    /// <summary>
    /// Сообщает об успешной обработке batch.
    /// </summary>
    public void Complete(Guid batchId, int eventCount) =>
        GetSignal(batchId).TrySetResult(new PacketBatchProcessingResult(batchId, eventCount));

    /// <summary>
    /// Передаёт тесту исключение, возникшее при обработке batch.
    /// </summary>
    public void Fail(Guid batchId, Exception exception) => GetSignal(batchId).TrySetException(exception);

    private TaskCompletionSource<PacketBatchProcessingResult> GetSignal(Guid batchId) =>
        _signals.GetOrAdd(batchId, static _ => 
                              new TaskCompletionSource<PacketBatchProcessingResult>(TaskCreationOptions.RunContinuationsAsynchronously));
}