namespace RedBlacksmith.TrafficProcessing.Kafka;

internal sealed class KafkaConsumerReadinessState
{
    private int _workerStarted;
    private int _assignedPartitions;

    /// <summary>
    /// Показывает, запущен ли Consumer Worker.
    /// </summary>
    public bool IsWorkerStarted => Volatile.Read(ref _workerStarted) == 1;

    /// <summary>
    /// Возвращает количество назначенных Consumer партиций.
    /// </summary>
    public int AssignedPartitions => Volatile.Read(ref _assignedPartitions);

    /// <summary>
    /// Отмечает запуск Consumer Worker.
    /// </summary>
    public void MarkStarted()
    {
        Volatile.Write(ref _assignedPartitions, 0);
        Volatile.Write(ref _workerStarted, 1);
    }

    /// <summary>
    /// Обновляет количество назначенных партиций.
    /// </summary>
    public void SetAssignedPartitions(int count) =>
        Volatile.Write(ref _assignedPartitions, Math.Max(0, count));

    /// <summary>
    /// Отмечает остановку Consumer Worker.
    /// </summary>
    public void MarkStopped()
    {
        Volatile.Write(ref _assignedPartitions, 0);
        Volatile.Write(ref _workerStarted, 0);
    }
}