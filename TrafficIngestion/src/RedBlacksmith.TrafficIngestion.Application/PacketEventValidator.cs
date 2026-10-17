using System.Diagnostics.CodeAnalysis;
using System.Net;
using Google.Protobuf;
using RedBlacksmith.TrafficIngestion.Models;
using Proto = RedBlacksmith.TrafficIngestion.Contracts;

namespace RedBlacksmith.TrafficIngestion.Application;

/// <summary>
/// Проверяет корректность входящих сетевых событий.
/// </summary>
internal static class PacketEventValidator
{
    /// <summary>
    /// Проверяет событие и преобразует его во внутреннюю модель.
    /// </summary>
    public static bool TryValidate(
        Proto.PacketEvent request,
        [NotNullWhen(true)] out PacketEvent? packetEvent,
        out string errorMessage)
    {
        packetEvent = null;
        errorMessage = string.Empty;

        if (!Guid.TryParseExact(request.EventId, "D", out var eventId) || eventId == Guid.Empty)
        {
            errorMessage = "EventId должен содержать непустой UUID.";
            return false;
        }

        if (!Guid.TryParseExact(request.SourceId, "D", out var sourceId) || sourceId == Guid.Empty)
        {
            errorMessage = "SourceId должен содержать непустой UUID.";
            return false;
        }

        if (request.OccurredAtUtc is null || !request.OccurredAtUtc.IsValid())
        {
            errorMessage = "OccurredAtUtc должен содержать корректную временную метку.";
            return false;
        }

        if (!IsValidIpAddress(request.SourceIp))
        {
            errorMessage = "SourceIp должен содержать 4 байта для IPv4 или 16 байт для IPv6.";
            return false;
        }

        if (!IsValidIpAddress(request.DestinationIp))
        {
            errorMessage = "DestinationIp должен содержать 4 байта для IPv4 или 16 байт для IPv6.";
            return false;
        }

        if (request is { HasSourcePort: true, SourcePort: > ushort.MaxValue })
        {
            errorMessage = "SourcePort должен находиться в диапазоне 0–65535.";
            return false;
        }

        if (request is { HasDestinationPort: true, DestinationPort: > ushort.MaxValue })
        {
            errorMessage = "DestinationPort должен находиться в диапазоне 0–65535.";
            return false;
        }

        if (request.Protocol > byte.MaxValue)
        {
            errorMessage = "Protocol должен находиться в диапазоне 0–255.";
            return false;
        }

        if (request.PacketLengthBytes == 0)
        {
            errorMessage = "PacketLengthBytes должен быть больше нуля.";
            return false;
        }

        packetEvent = CreatePacketEvent(request, eventId, sourceId);

        return true;
    }

    /// <summary>
    /// Создает новый экземпляр <see cref="PacketEvent"/> из данных Protobuf запроса.
    /// </summary>
    /// <param name="request">Protobuf запрос <see cref="RedBlacksmith.TrafficIngestion.Contracts.PacketEvent"/></param>
    /// <param name="eventId"></param>
    /// <param name="sourceId"></param>
    private static PacketEvent CreatePacketEvent(Proto.PacketEvent request, Guid eventId, Guid sourceId)
    {
        return new PacketEvent
        {
            EventId = eventId,
            SourceId = sourceId,
            OccurredAtUtc = request.OccurredAtUtc.ToDateTimeOffset(),
            SourceIp = new IPAddress(request.SourceIp.ToByteArray()),
            DestinationIp = new IPAddress(request.DestinationIp.ToByteArray()),
            SourcePort = request.HasSourcePort ? (ushort)request.SourcePort : null,
            DestinationPort = request.HasDestinationPort ? (ushort)request.DestinationPort : null,
            Protocol = (byte)request.Protocol,
            PacketLengthBytes = request.PacketLengthBytes
        };
    }

    /// <summary>
    /// Проверяет длину бинарного представления IP-адреса.
    /// </summary>
    private static bool IsValidIpAddress(ByteString address) =>
        address.Length is 4 or 16;
}