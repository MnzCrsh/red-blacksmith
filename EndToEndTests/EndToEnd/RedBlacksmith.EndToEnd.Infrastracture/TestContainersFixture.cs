using Confluent.Kafka;
using Confluent.Kafka.Admin;
using Testcontainers.ClickHouse;
using Testcontainers.Kafka;
using Testcontainers.PostgreSql;
using Xunit;

namespace RedBlacksmith.EndToEnd;

public class TestContainersFixture : IAsyncLifetime
{
    private const           string   PostgreSqlImage = "postgres:16-alpine";
    private const           string   KafkaImage      = "confluentinc/cp-kafka:7.5.12";
    private const           string   ClickHouseImage = "clickhouse/clickhouse-server:24.8";
    
    private static readonly TimeSpan StartupTimeout  = TimeSpan.FromSeconds(90);
    
    /// <summary>
    /// Имя основного Kafka-топика.
    /// </summary>
    public const string PacketEventsTopic = "network.packet-events.v1";
    
    /// <summary>
    /// Имя Kafka-топика для провалившихся сообщений.
    /// </summary>
    public const string DeadLetterTopic = "network.packet-events-dlq.v1";
    
    private readonly PostgreSqlContainer _postgreSql =
        new PostgreSqlBuilder(PostgreSqlImage)
            .WithDatabase("traffic_tests")
            .WithUsername("postgres")
            .WithPassword("postgres")
            .Build();

    private readonly KafkaContainer _kafka = new KafkaBuilder(KafkaImage).Build();

    private readonly ClickHouseContainer _clickHouse = new ClickHouseBuilder(ClickHouseImage).Build();
    
    /// <summary>
    /// Строка подключения к PostgreSQL.
    /// </summary>
    public string PostgreSqlConnectionString => _postgreSql.GetConnectionString();

    /// <summary>
    /// Адрес Kafka-брокера.
    /// </summary>
    public string KafkaBootstrapServers => _kafka.GetBootstrapAddress();

    /// <summary>
    /// Строка подключения к ClickHouse.
    /// </summary>
    public string ClickHouseConnectionString => _clickHouse.GetConnectionString();
    
    /// <summary>
    /// Создаёт необходимые Kafka-топики.
    /// </summary>
    private async Task CreateKafkaTopicsAsync()
    {
        using var adminClient = new AdminClientBuilder(new AdminClientConfig { BootstrapServers = KafkaBootstrapServers }).Build();

        await adminClient.CreateTopicsAsync(
        [
            new TopicSpecification
            {
                Name = PacketEventsTopic,
                NumPartitions = 1,
                ReplicationFactor = 1
            },
            new TopicSpecification
            {
                Name = DeadLetterTopic,
                NumPartitions = 1,
                ReplicationFactor = 1
            }
        ]);
    }

    public async ValueTask InitializeAsync()
    {
        using var timeoutCts = new CancellationTokenSource(StartupTimeout);

        try
        {
            await Task.WhenAll(_postgreSql.StartAsync(timeoutCts.Token),
                               _kafka.StartAsync(timeoutCts.Token),
                               _clickHouse.StartAsync(timeoutCts.Token));

            await CreateKafkaTopicsAsync();
        }
        catch (OperationCanceledException exception) when (timeoutCts.IsCancellationRequested)
        {
            await DisposeAsync();

            throw new TimeoutException($"Тестовая инфраструктура не запустилась за {StartupTimeout}.", exception);
        }
        catch
        {
            await DisposeAsync();
            throw;
        }
    }
    
    public async ValueTask DisposeAsync() => 
        await Task.WhenAll(_clickHouse.DisposeAsync().AsTask(), _kafka.DisposeAsync().AsTask(), _postgreSql.DisposeAsync().AsTask());
}
