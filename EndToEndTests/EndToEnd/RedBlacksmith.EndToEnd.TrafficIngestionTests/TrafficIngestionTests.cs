using System.ComponentModel;
using System.Net;
using Google.Protobuf;
using Google.Protobuf.WellKnownTypes;
using Grpc.Net.Client;
using RedBlacksmith.TrafficIngestion.Contracts;
using Xunit;

namespace RedBlacksmith.EndToEnd.TrafficIngestionTests;

[Collection("TrafficIngestion")]
public class TrafficIngestionTests(TestContainersFixture fixture)
{
    [Fact, DisplayName("TrafficIngestion service should publish batch to Kafka")]
    public async Task Ingest_ValidEvent_PublishesBatchToKafka()
    {
        const string sourceIp = "192.168.1.10";
        const string destinationIp = "192.168.1.20";
        const int sourcePort = 54321;
        const int destinationPort = 443;
        const int protocol = 6;
        const int packetLengthBytes = 1024;

        await fixture.StartTrafficIngestionAsync();

        using var channel = GrpcChannel.ForAddress(fixture.TrafficIngestionAddress);

        using var consumer = new KafkaTestConsumer(fixture.KafkaBootstrapServers, TestContainersFixture.PacketEventsTopic);
        
        var client = new global::RedBlacksmith.TrafficIngestion.Contracts.TrafficIngestion.TrafficIngestionClient(channel);

        using var cancellationTokenSource = new CancellationTokenSource(
            TimeSpan.FromSeconds(30));
 
        var eventId = Guid.CreateVersion7();
        var sourceId = Guid.CreateVersion7();

        var packetEvent = new PacketEvent
        {
            EventId = eventId.ToString(),
            SourceId = sourceId.ToString(),
            OccurredAtUtc = Timestamp.FromDateTime(DateTime.UtcNow),
            SourceIp = ByteString.CopyFrom(IPAddress.Parse(sourceIp).GetAddressBytes()),
            DestinationIp = ByteString.CopyFrom(IPAddress.Parse(destinationIp).GetAddressBytes()),
            SourcePort = sourcePort,
            DestinationPort = destinationPort,
            Protocol = protocol,
            PacketLengthBytes = packetLengthBytes
        };

        using var call = client.Ingest(
            cancellationToken: cancellationTokenSource.Token);

        await call.RequestStream.WriteAsync(packetEvent, cancellationTokenSource.Token);
        await call.RequestStream.CompleteAsync();

        await call.ResponseAsync;

        using var batch = await consumer.ConsumeUntilEventAsync(
            eventId,
            cancellationTokenSource.Token);

        var actualEvent = batch.RootElement
                               .GetProperty("items")
                               .EnumerateArray()
                               .Single(item => item.GetProperty(nameof(eventId)).GetGuid() == eventId);

        Assert.Equal(sourceId, actualEvent.GetProperty(nameof(sourceId)).GetGuid());
        Assert.Equal(sourceIp, actualEvent.GetProperty(nameof(sourceIp)).GetString());
        Assert.Equal(destinationIp, actualEvent.GetProperty(nameof(destinationIp)).GetString());
        Assert.Equal(sourcePort, actualEvent.GetProperty(nameof(sourcePort)).GetUInt16());
        Assert.Equal(destinationPort, actualEvent.GetProperty(nameof(destinationPort)).GetUInt16());
        Assert.Equal(protocol, actualEvent.GetProperty(nameof(protocol)).GetByte());
        Assert.Equal(1024u, actualEvent.GetProperty(nameof(packetLengthBytes)).GetUInt32());
    }
}