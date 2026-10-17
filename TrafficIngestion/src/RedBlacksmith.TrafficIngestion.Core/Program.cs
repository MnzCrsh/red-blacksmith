using System.Threading.Channels;
using Confluent.Kafka;
using RedBlacksmith.TrafficIngestion.Models;
using RedBlacksmith.TrafficIngestion.Application;

var builder = WebApplication.CreateBuilder(args);

var services = builder.Services;
services.AddGrpc();

var channel = Channel.CreateBounded<PacketEvent>(new BoundedChannelOptions(10_000)
{
    FullMode = BoundedChannelFullMode.Wait,
    SingleReader = true,
    SingleWriter = false,
    AllowSynchronousContinuations = false
});

var producerConfig = new ProducerConfig
{
    BootstrapServers = builder.Configuration["Kafka:BootstrapServers"],
    Acks = Acks.All,
    EnableIdempotence = true,
    LingerMs = 5,
    CompressionType = CompressionType.Lz4
};

services.AddSingleton(channel.Reader)
        .AddSingleton(channel.Writer)
        .AddSingleton<IProducer<Null, byte[]>>(_ => new ProducerBuilder<Null, byte[]>(producerConfig)
                                                     .SetKeySerializer(Serializers.Null)
                                                     .SetValueSerializer(Serializers.ByteArray)
                                                     .Build())
        .AddTrafficIngestionApplication();

var app = builder.Build();

app.MapGrpcService<TrafficIngestionGrpcService>();

app.MapGet("/", () => "gRpc client allowed only.");

app.Run();

