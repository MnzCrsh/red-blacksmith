using System.Net;

namespace RedBlacksmith.TrafficIngestion.Models;

/// <summary>
/// Модель сетевого пакета.
/// </summary>
internal record PacketEvent
{
    /// <summary>
    /// Идентификатор события.
    /// </summary>
    public Guid EventId { get; init; }

    /// <summary>
    /// Идентификатор пакета.
    /// </summary>
    public Guid SourceId { get; init; }
    
    public DateTimeOffset OccurredAtUtc { get; init; }

    /// <summary>
    /// IP от которого получен пакет. 
    /// </summary>
    public IPAddress SourceIp { get; init; } = null!;

    /// <summary>
    /// IP к которому был послан пакет.
    /// </summary>
    public IPAddress DestinationIp { get; init; } = null!;

    /// <summary>
    /// Порт по которому был отправлен запрос.
    /// </summary>
    public ushort? SourcePort { get; init; }

    /// <summary>
    /// Порт к которому был послан пакет.
    /// </summary>
    public ushort? DestinationPort { get; init; }

    /// <summary>
    /// Протокол использовавшийся для отправки пакета.
    /// </summary>
    public byte Protocol { get; init; }

    /// <summary>
    /// Размер пакета в байтах.
    /// </summary>
    public uint PacketLengthBytes { get; init; }
}