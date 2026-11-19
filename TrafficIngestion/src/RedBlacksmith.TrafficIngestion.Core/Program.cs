using System.Threading.Channels;
using Confluent.Kafka;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using RedBlacksmith;
using RedBlacksmith.HealthChecks;
using RedBlacksmith.TrafficIngestion.Models;
using RedBlacksmith.TrafficIngestion.Application;

var builder = WebApplication.CreateBuilder(args);


builder.WebHost.ConfigureKestrel(options =>
{
    options.ListenAnyIP(8080, listenOptions => listenOptions.Protocols = HttpProtocols.Http2);
    options.ListenAnyIP(8081, listenOptions => listenOptions.Protocols = HttpProtocols.Http1);
});

var services = builder.Services;
services.AddGrpc();

var channel = Channel.CreateBounded<PacketEvent>(new BoundedChannelOptions(10_000)
{
    FullMode = BoundedChannelFullMode.Wait,
    SingleReader = true,
    SingleWriter = false,
    AllowSynchronousContinuations = false
});
var bootstrapServers = builder.Configuration["Kafka:BootstrapServers"];
var producerConfig = new ProducerConfig
{
    BootstrapServers = bootstrapServers,
    Acks = Acks.All,
    EnableIdempotence = true,
    LingerMs = 5,
    CompressionType = CompressionType.Lz4
};

services.AddObservability();
services.AddSingleton(channel.Reader)
        .AddSingleton(channel.Writer)
        .AddSingleton<IAdminClient>(_ => new AdminClientBuilder(new AdminClientConfig
        {
            BootstrapServers = bootstrapServers
        }).Build())
        .AddSingleton<IProducer<Null, byte[]>>(_ => new ProducerBuilder<Null, byte[]>(producerConfig)
                                                     .SetKeySerializer(Serializers.Null)
                                                     .SetValueSerializer(Serializers.ByteArray)
                                                     .Build())
        .AddTrafficIngestionApplication()
        .AddIngestionHealthChecks();

var app = builder.Build();

app.MapGrpcService<TrafficIngestionGrpcService>();

app.MapGet("/", () => "gRpc client allowed only.");

app.Run();

public partial class Program { }
