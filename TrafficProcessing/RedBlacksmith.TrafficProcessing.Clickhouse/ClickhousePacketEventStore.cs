using System.Net;
using System.Net.Sockets;
using ClickHouse.Driver;
using Microsoft.Extensions.Logging;
using RedBlacksmith.TrafficProcessing.Db.Models;
using RedBlacksmith.TrafficProcessing.Models.Abstracts;
using RedBlacksmith.TrafficProcessing.Models.DomainModels;

namespace RedBlacksmith.TrafficProcessing.Db;

/// <inheritdoc/>
internal class ClickhousePacketEventStore(
    ClickHouseClient client,
    ClickHouseOptions options,
    ILogger<ClickhousePacketEventStore> logger) : IPacketEventStore
{
    private readonly string _tableName = $"`{options.Database}`.`packet_events`";
    
    /// <inheritdoc/>
    public async Task AppendBatchAsync(Guid batchId, IReadOnlyList<PacketEvent> events, CancellationToken cancellationToken)
    {
        Validate(batchId, events);
        cancellationToken.ThrowIfCancellationRequested();
        
        var insertOptions = CreateInsertOptions(batchId, events);
        var rows = MapRows(batchId, events);
        
        var insertedRows = await client.InsertBinaryAsync(_tableName, rows, insertOptions, cancellationToken);

        logger.LogDebug("ClickHouse batch insert completed. BatchId: {BatchId}, Events: {EventCount}, Rows reported by ClickHouse: {InsertedRows}.",
                        batchId, events.Count, insertedRows);
    }

    private static InsertOptions CreateInsertOptions(Guid batchId, IReadOnlyList<PacketEvent> events) =>
        new()
        {
            BatchSize = events.Count,
            MaxDegreeOfParallelism = 1,
            CustomSettings = new Dictionary<string, object>
            {
                ["insert_deduplicate"] = 1,
                ["insert_deduplication_token"] = batchId.ToString("N")
            }
        };

    private static void Validate(Guid batchId, IReadOnlyList<PacketEvent> events)
    {
        if (batchId == Guid.Empty)
            throw new ArgumentException("Идентификатор пакета не может быть пустым.", nameof(batchId));

        if (events.Count is 0)
            throw new ArgumentException("Пакет должен содержать хотя бы одно событие.", nameof(events));
    }

    /// <summary>
    /// Проецирует <see cref="PacketEvent"/> в <see cref="ClickHousePacketEventRow"/>.
    /// </summary>
    /// <param name="batchId">Идентификатор батча из Kafka.</param>
    /// <param name="events">Коллекция событий для проекции.</param>
    private static IEnumerable<ClickHousePacketEventRow> MapRows(Guid batchId, IReadOnlyList<PacketEvent> events) =>
        events.Select(packetEvent => new ClickHousePacketEventRow
        {
            EventId = packetEvent.EventId,
            BatchId = batchId,
            SourceId = packetEvent.SourceId,
            OccurredAtUtc = packetEvent.OccurredAtUtc.UtcDateTime,
            SourceIp = NormalizeIpAddress(packetEvent.SourceIp),
            DestinationIp = NormalizeIpAddress(packetEvent.DestinationIp),
            SourcePort = packetEvent.SourcePort,
            DestinationPort = packetEvent.DestinationPort,
            Protocol = packetEvent.Protocol,
            PacketLengthBytes = packetEvent.PacketLengthBytes
        });

    /// <summary>
    /// Приводит IPv4-адрес к IPv4-mapped IPv6 для хранения в колонке IPv6.
    /// </summary>
    private static IPAddress NormalizeIpAddress(IPAddress address) =>
        address.AddressFamily is AddressFamily.InterNetwork 
            ? address.MapToIPv6()
            : address;
}