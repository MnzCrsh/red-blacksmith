using System.Net;
using Google.Protobuf;
using Google.Protobuf.WellKnownTypes;
using RedBlacksmith.TrafficIngestion.Contracts;
using Xunit;

namespace RedBlacksmith.EndToEnd.TrafficIngestionTests;

public class TrafficIngestionTests(TestContainersFixture fixture)
{
    [Fact]
    public async Task Ingest_ValidEvent_PublishesBatchToKafka()
    {
        const string sourceIp = "192.168.1.10";
        const string destinationIp = "192.168.1.20";
        await using var factory = new TrafficIngestionFactory(fixture.KafkaBootstrapServers, TestContainersFixture.PacketEventsTopic);

        using var channel = factory.CreateGrpcChannel();

        using var consumer = new KafkaTestConsumer(fixture.KafkaBootstrapServers, TestContainersFixture.PacketEventsTopic);

        // var client = new TrafficIngestionClient(channel);
        //
        // using var cancellationTokenSource = new CancellationTokenSource(
        //     TimeSpan.FromSeconds(15));
        //
        // var eventId = Guid.CreateVersion7();
        // var sourceId = Guid.CreateVersion7();
        //
        // var packetEvent = new PacketEvent
        // {
        //     EventId = eventId.ToString(),
        //     SourceId = sourceId.ToString(),
        //     OccurredAtUtc = Timestamp.FromDateTime(DateTime.UtcNow),
        //     SourceIp = ByteString.CopyFrom(IPAddress.Parse(sourceIp).GetAddressBytes()),
        //     DestinationIp = ByteString.CopyFrom(IPAddress.Parse(destinationIp).GetAddressBytes()),
        //     SourcePort = 54321,
        //     DestinationPort = 443,
        //     Protocol = 6,
        //     PacketLengthBytes = 1024
        // };
        //
        // using var call = client.Ingest(
        //     cancellationToken: cancellationTokenSource.Token);
        //
        // await call.RequestStream.WriteAsync(packetEvent);
        // await call.RequestStream.CompleteAsync();
        //
        // await call.ResponseAsync;
        //
        // using var batch = await consumer.ConsumeUntilEventAsync(eventId, cancellationTokenSource.Token);
        //
        // var actualEvent = batch.RootElement
        //                        .GetProperty("items")
        //                        .EnumerateArray()
        //                        .Single(item => item.GetProperty("eventId").GetGuid() == eventId);
        //
        // Assert.Equal(sourceId, actualEvent.GetProperty("sourceId").GetGuid());
        // Assert.Equal(sourceIp, actualEvent.GetProperty(nameof(sourceIp)).GetString());
        // Assert.Equal(destinationIp, actualEvent.GetProperty(nameof(destinationIp)).GetString());
        // Assert.Equal(54321, actualEvent.GetProperty("sourcePort").GetUInt16());
        // Assert.Equal(443, actualEvent.GetProperty("destinationPort").GetUInt16());
        // Assert.Equal(6, actualEvent.GetProperty("protocol").GetByte());
        // Assert.Equal(1024u, actualEvent.GetProperty("packetLengthBytes").GetUInt32());
    }
}