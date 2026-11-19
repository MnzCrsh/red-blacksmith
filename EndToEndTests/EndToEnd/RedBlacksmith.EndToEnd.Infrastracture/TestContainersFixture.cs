using Confluent.Kafka;
using Confluent.Kafka.Admin;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using DotNet.Testcontainers.Networks;
using Testcontainers.ClickHouse;
using Testcontainers.Kafka;
using Testcontainers.PostgreSql;
using Xunit;

namespace RedBlacksmith.EndToEnd;

public class TestContainersFixture : IAsyncLifetime
{
    private const           string   PostgreSqlImage = "postgres:16-alpine";
    private const           string   KafkaImage      = "confluentinc/cp-kafka:7.5.12";
    private const           string   ClickHouseImage = "clickhouse/clickhouse-server:26.8";
    
    private const string TrafficIngestionImage         = "redblacksmith/traffic-ingestion:local";
    private const string TrafficProcessingImage        = "redblacksmith/traffic-processing:local";
    
    private const string KafkaInternalBootstrapServers = "kafka:29092";
    private const string TrafficProcessingGroupId      = "traffic-processing-e2e";
    
    private const ushort TrafficIngestionPort  = 8080;
    private const ushort TrafficProcessingPort = 8081;
    
    private static readonly TimeSpan StartupTimeout  = TimeSpan.FromSeconds(90);
    
    /// <summary>
    /// Локальная сеть.
    /// </summary>
    private readonly INetwork _network;
    private bool _networkCreated;
    
    /// <summary>
    /// Имя основного Kafka-топика.
    /// </summary>
    public const string PacketEventsTopic = "network.packet-events.v1";
    
    /// <summary>
    /// Имя Kafka-топика для провалившихся сообщений.
    /// </summary>
    public const string DeadLetterTopic = "network.packet-events-dlq.v1";
    
    private readonly KafkaContainer _kafka;
    private readonly PostgreSqlContainer _postgreSql;
    private readonly ClickHouseContainer _clickHouse;
    
    private IContainer? _trafficIngestion;
    private IContainer? _trafficProcessing;

    public TestContainersFixture()
    {
        _network = new NetworkBuilder()
                   .WithName($"redblacksmith-e2e-{Guid.NewGuid():N}")
                   .Build();

        _postgreSql = new PostgreSqlBuilder(PostgreSqlImage)
                      .WithNetwork(_network)
                      .WithNetworkAliases("postgres")
                      .WithDatabase("traffic_tests")
                      .WithUsername("postgres")
                      .WithPassword("postgres")
                      .Build();
        
        _kafka = new KafkaBuilder(KafkaImage)
                 .WithKRaft()
                 .WithNetwork(_network)
                 .WithListener(KafkaInternalBootstrapServers)
                 .Build();

        _clickHouse = new ClickHouseBuilder(ClickHouseImage)
                      .WithNetwork(_network)
                      .WithNetworkAliases("clickhouse")
                      .Build();
        
    }
    
    /// <summary>
    /// Строка подключения к PostgreSQL.
    /// </summary>
    public string PostgreSqlConnectionString => $"{_postgreSql.GetConnectionString()};GSS Encryption Mode=Disable";
    
    /// <summary>
    /// Внутренняя строка подключения к PostgreSQL для Docker-контейнеров.
    /// </summary>
    public string PostgreSqlInternalConnectionString => "Host=postgres;Port=5432;Database=traffic_tests;Username=postgres;Password=postgres;GSS Encryption Mode=Disable";

    /// <summary>
    /// Адрес Kafka-брокера.
    /// </summary>
    public string KafkaBootstrapServers => _kafka.GetBootstrapAddress();

    /// <summary>
    /// Строка подключения к ClickHouse.
    /// </summary>
    public string ClickHouseConnectionString => _clickHouse.GetConnectionString();
    
    public string ClickHouseHttpEndpoint => $"http://{_clickHouse.Hostname}:{_clickHouse.GetMappedPublicPort(8123)}";
    
    /// <summary>
    /// Внутренняя строка подключения к ClickHouse для Docker-контейнеров.
    /// </summary>
    public string ClickHouseInternalConnectionString => "Host=clickhouse;Port=8123;Username=clickhouse;Password=clickhouse;Database=traffic;JsonReadMode=Binary";
    
    public Uri TrafficIngestionAddress
    {
        get
        {
            var container = _trafficIngestion 
                            ?? throw new InvalidOperationException("TrafficIngestion ещё не запущен.");

            return new UriBuilder(Uri.UriSchemeHttp, container.Hostname, container.GetMappedPublicPort(TrafficIngestionPort)).Uri;
        }
    }
    
    /// <summary>
    /// Создаёт необходимые Kafka-топики.
    /// </summary>
    private async Task CreateKafkaTopicsAsync() =>
        await new AdminClientBuilder(new AdminClientConfig { BootstrapServers = KafkaBootstrapServers })
              .Build()
              .CreateTopicsAsync(
              [
                  new TopicSpecification { Name = PacketEventsTopic, NumPartitions = 1, ReplicationFactor = 1 },
                  new TopicSpecification { Name = DeadLetterTopic, NumPartitions = 1, ReplicationFactor = 1 }
              ]);

    /// <summary>
    /// Запускает контейнер TrafficIngestion в общей Docker-сети.
    /// </summary>
    public async Task StartTrafficIngestionAsync()
    {
        if (_trafficIngestion is not null)
        {
            return;
        }

        using var timeoutCts = new CancellationTokenSource(StartupTimeout);

        var container = new ContainerBuilder(TrafficIngestionImage)
                        .WithNetwork(_network)
                        .WithEnvironment("Kafka__BootstrapServers", KafkaInternalBootstrapServers)
                        .WithEnvironment("Kafka__Topic", PacketEventsTopic)
                        .WithPortBinding(TrafficIngestionPort, true)
                        .WithWaitStrategy(
                                          Wait.ForUnixContainer()
                                              .UntilInternalTcpPortIsAvailable(TrafficIngestionPort))
                        .Build();

        try
        {
            await container.StartAsync(timeoutCts.Token);

            _trafficIngestion = container;
        }
        catch (OperationCanceledException exception) when (timeoutCts.IsCancellationRequested)
        {
            await container.DisposeAsync();
            throw new TimeoutException($"TrafficIngestion не запустился за {StartupTimeout}.", exception);
        }
        catch
        {
            await container.DisposeAsync();
            throw;
        }
    }
    
    /// <summary>
    /// Запускает контейнер TrafficProcessing в общей Docker-сети.
    /// </summary>
    public async Task StartTrafficProcessingAsync()
    {
        if (_trafficProcessing is not null)
        {
            return;
        }

        using var timeoutCts = new CancellationTokenSource(StartupTimeout);

        var container = new ContainerBuilder(TrafficProcessingImage)
                        .WithNetwork(_network)
                        .WithNetworkAliases("traffic-processing")
                        .WithEnvironment("Kafka__BootstrapServers", KafkaInternalBootstrapServers)
                        .WithEnvironment("Kafka__Topic", PacketEventsTopic)
                        .WithEnvironment("Kafka__DlqTopic", DeadLetterTopic)
                        .WithEnvironment("Kafka__GroupId", TrafficProcessingGroupId)
                        .WithEnvironment("ClickHouse__ConnectionString", ClickHouseInternalConnectionString)
                        .WithEnvironment("ClickHouse__Database", "traffic")
                        .WithEnvironment("Postgres__ConnectionString", PostgreSqlInternalConnectionString)
                        .WithWaitStrategy(Wait.ForUnixContainer().UntilInternalTcpPortIsAvailable(TrafficProcessingPort))
                        .Build();

        try
        {
            await container.StartAsync(timeoutCts.Token);
            _trafficProcessing = container;
        }
        catch (OperationCanceledException exception) when (timeoutCts.IsCancellationRequested)
        {
            await container.DisposeAsync();
            throw new TimeoutException($"TrafficProcessing не запустился за {StartupTimeout}.",
                                       exception);
        }
        catch
        {
            await container.DisposeAsync();
            throw;
        }
    }
    
    public async ValueTask InitializeAsync()
    {
        using var timeoutCts = new CancellationTokenSource(StartupTimeout);
        try
        {
            await _network.CreateAsync(timeoutCts.Token);
            _networkCreated = true;

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
    
    public async ValueTask DisposeAsync()
    {
        var trafficIngestion = _trafficIngestion;
        _trafficIngestion = null;
        
        var trafficProcessing = _trafficProcessing;
        _trafficProcessing = null;

        List<Task> disposeTasks =
        [
            _clickHouse.DisposeAsync().AsTask(),
            _kafka.DisposeAsync().AsTask(),
            _postgreSql.DisposeAsync().AsTask()
        ];

        if (trafficIngestion is not null)
        {
            disposeTasks.Add(trafficIngestion.DisposeAsync().AsTask());
        }

        if (trafficProcessing is not null)
        {
            disposeTasks.Add(trafficProcessing.DisposeAsync().AsTask());
        }

        try
        {
            await Task.WhenAll(disposeTasks);
        }
        finally
        {
            if (_networkCreated)
            {
                await _network.DisposeAsync();
                _networkCreated = false;
            }
        }
    }
    
    public async Task<string> GetTrafficProcessingLogsAsync(CancellationToken cancellationToken = default)
    {
        var container = _trafficProcessing
                        ?? throw new InvalidOperationException("TrafficProcessing ещё не запущен.");

        var (stdout, stderr) = await container.GetLogsAsync(ct: cancellationToken);

        return $"STDOUT:{Environment.NewLine}{stdout}{Environment.NewLine}STDERR:{Environment.NewLine}{stderr}";
    }
}
