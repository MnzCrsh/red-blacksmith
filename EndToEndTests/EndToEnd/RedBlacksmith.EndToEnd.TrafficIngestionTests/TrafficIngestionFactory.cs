using Grpc.Net.Client;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace RedBlacksmith.EndToEnd.TrafficIngestionTests;

internal sealed class TrafficIngestionFactory(string bootstrapServers, string topic) : WebApplicationFactory<Program>
{
    private readonly string _bootstrapServers = !string.IsNullOrWhiteSpace(bootstrapServers) 
        ? bootstrapServers 
        : throw new ArgumentException("Не указан адрес Kafka.", nameof(bootstrapServers));
    
    private readonly string _topic =
        !string.IsNullOrWhiteSpace(topic)
            ? topic
            : throw new ArgumentException("Не указан Kafka-топик.", nameof(topic));
    

    /// <inheritdoc />
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureAppConfiguration((_, configuration) =>
        {
            configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Kafka:BootstrapServers"] = _bootstrapServers,
                ["Kafka:Topic"] = _topic
            });
        });
    }
    
    /// <summary>
    /// Создаёт gRPC-канал, подключённый к тестовому серверу.
    /// </summary>
    public GrpcChannel CreateGrpcChannel() =>
        GrpcChannel.ForAddress("http://localhost", new GrpcChannelOptions { HttpHandler = Server.CreateHandler() });
}
