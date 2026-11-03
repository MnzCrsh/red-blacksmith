using System.Net;
using ClickHouse.Driver;

namespace RedBlacksmith.TrafficProcessing.Db.Models;

internal sealed class ClickHousePacketEventRow
{
    /// <summary>
    /// Идентификатор сетевого события.
    /// </summary>
    [ClickHouseColumn(Name = "event_id", Type = "UUID")]
    public Guid EventId { get; init; }

    /// <summary>
    /// Идентификатор батча, полученного из Kafka.
    /// </summary>
    [ClickHouseColumn(Name = "batch_id", Type = "UUID")]
    public Guid BatchId { get; init; }

    /// <summary>
    /// Идентификатор источника трафика.
    /// </summary>
    [ClickHouseColumn(Name = "source_id", Type = "UUID")]
    public Guid SourceId { get; init; }

    /// <summary>
    /// Время возникновения события в UTC.
    /// </summary>
    [ClickHouseColumn(Name = "occurred_at_utc", Type = "DateTime64(7, 'UTC')")]
    public DateTime OccurredAtUtc { get; init; }

    /// <summary>
    /// IP отправителя пакета.
    /// </summary>
    [ClickHouseColumn(Name = "source_ip", Type = "IPv6")]
    public IPAddress SourceIp { get; init; } = null!;

    /// <summary>
    /// IP получателя пакета.
    /// </summary>
    [ClickHouseColumn(Name = "destination_ip", Type = "IPv6")]
    public IPAddress DestinationIp { get; init; } = null!;

    /// <summary>
    /// Порт отправителя.
    /// </summary>
    [ClickHouseColumn(Name = "source_port", Type = "Nullable(UInt16)")]
    public ushort? SourcePort { get; init; }

    /// <summary>
    /// Порт получателя.
    /// </summary>
    [ClickHouseColumn(Name = "destination_port", Type = "Nullable(UInt16)")]
    public ushort? DestinationPort { get; init; }

    /// <summary>
    /// Номер сетевого протокола.
    /// </summary>
    [ClickHouseColumn(Name = "protocol", Type = "UInt8")]
    public byte Protocol { get; init; }

    /// <summary>
    /// Размер пакета в байтах.
    /// </summary>
    [ClickHouseColumn(Name = "packet_length_bytes", Type = "UInt32")]
    public uint PacketLengthBytes { get; init; }
}