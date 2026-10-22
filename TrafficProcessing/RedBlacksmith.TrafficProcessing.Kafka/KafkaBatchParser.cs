using System.Net;
using System.Text.Json;
using RedBlacksmith.TrafficProcessing.Models;

namespace RedBlacksmith.TrafficProcessing.Kafka;

internal class KafkaBatchParser
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static (Guid batchId, DateTimeOffset CreatedAtUtc, PacketEvent[] events) Parse(ReadOnlySpan<byte> payload)
    {
        if (payload.IsEmpty)
        {
            throw new InvalidKafkaBatchException("Kafka message error: Payload is empty");
        }

        var message = ValidateKafkaMessage(payload, out var items);

        var events = new PacketEvent[items.Length];
        var eventIds = new HashSet<Guid>(items.Length);

        for (var i = 0; i < items.Length; i++)
        {
            var item = items[i];
            var sourceIp = ValidatePacketEventItem(item, i, eventIds, out var destinationIp);

            events[i] = new PacketEvent(item.EventId,
                                        item.SourceId,
                                        item.OccurredAtUtc.ToUniversalTime(),
                                        sourceIp,
                                        destinationIp!,
                                        item.SourcePort,
                                        item.DestinationPort,
                                        item.Protocol,
                                        item.PacketLengthBytes);
        }
        return (message.BatchId, message.CreatedAtUtc, events);
    }

    private static IPAddress ValidatePacketEventItem(KafkaPacketEvent item, int i, HashSet<Guid> eventIds,
        out IPAddress? destinationIp)
    {
        if (item.EventId == Guid.Empty || item.SourceId == Guid.Empty)
        {
            throw new InvalidKafkaBatchException($"Event at index {i} contains an empty UUID.");
        }

        if (!eventIds.Add(item.EventId))
        {
            throw new InvalidKafkaBatchException($"EventId {item.EventId} occurs more than once in the batch.");
        }

        if (item.OccurredAtUtc == default)
        {
            throw new InvalidKafkaBatchException($"Event {item.EventId} has no timestamp.");
        }

        if (!IPAddress.TryParse(item.SourceIp, out var sourceIp))
        {
            throw new InvalidKafkaBatchException($"Event {item.EventId} contains an invalid source IP.");
        }   
            
        if (!IPAddress.TryParse(item.DestinationIp, out destinationIp))
        {
            throw new InvalidKafkaBatchException($"Event {item.EventId} contains an invalid destination IP.");
        }
            
        return item.PacketLengthBytes == 0 ? 
            throw new InvalidKafkaBatchException($"Event {item.EventId} has zero packet length.") 
            : sourceIp;
    }

    private static KafkaPacketEventBatch ValidateKafkaMessage(ReadOnlySpan<byte> payload,
        out KafkaPacketEvent[] kafkaPacketEvents)
    {
        KafkaPacketEventBatch message;
        try
        {
            message = JsonSerializer.Deserialize<KafkaPacketEventBatch>(payload, JsonOptions)
                      ?? throw new InvalidKafkaBatchException("Kafka message error: Contains a null batch.");
        }
        catch (JsonException e)
        {
            throw new InvalidKafkaBatchException("Kafka message contains invalid JSON or field values.", e);
        }
        catch (NotSupportedException e)
        {
            throw new InvalidKafkaBatchException("Kafka message contains an unsupported JSON structure.", e);
        }

        if (message.BatchId == Guid.Empty)
        {
            throw new InvalidKafkaBatchException("BatchId must be a non-empty UUID.");
        }

        if (message.CreatedAtUtc == default)
        {
            throw new InvalidKafkaBatchException("CreatedAtUtc is missing.");
        }

        if (message.Items is not {Length: > 0} items)
        {
            throw new InvalidKafkaBatchException("Kafka batch must contain at least one item.");
        }

        kafkaPacketEvents = items;
        return message;
    }
}