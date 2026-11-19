using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Confluent.Kafka;
using Npgsql;
using Xunit;

namespace RedBlacksmith.EndToEnd.TrafficProcessing;

[Collection("TrafficProcessing")]
public class TrafficProcessingTests(TestContainersFixture fixture)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    
    private const int ProcessingTimeoutSeconds = 45;
    
    [Fact]
    public async Task ProcessBatch_WithValidEvent_PersistsEventToBothDatabases()
    {
         await fixture.StartTrafficProcessingAsync();

        using var timeout = new CancellationTokenSource(
            TimeSpan.FromSeconds(ProcessingTimeoutSeconds));

        var batchId = Guid.NewGuid();
        var eventId = Guid.NewGuid();
        var sourceId = Guid.NewGuid();

        var occurredAtUtc = DateTimeOffset.UtcNow;
        var sourceIp = IPAddress.Parse("192.0.2.10");
        var destinationIp = IPAddress.Parse("198.51.100.20");

        const uint packetLengthBytes = 1500;

        var batch = new
        {
            BatchId = batchId,
            CreatedAtUtc = DateTimeOffset.UtcNow,
            Items = new[]
            {
                new
                {
                    EventId = eventId,
                    SourceId = sourceId,
                    OccurredAtUtc = occurredAtUtc,
                    SourceIp = sourceIp.ToString(),
                    DestinationIp = destinationIp.ToString(),
                    SourcePort = (ushort?)54321,
                    DestinationPort = (ushort?)443,
                    Protocol = (byte)6,
                    PacketLengthBytes = packetLengthBytes
                }
            }
        };

        using var producer = new ProducerBuilder<Null, byte[]>(
            new ProducerConfig
            {
                BootstrapServers = fixture.KafkaBootstrapServers,
                Acks = Acks.All
            }).Build();

        await producer.ProduceAsync(TestContainersFixture.PacketEventsTopic, 
                                    new Message<Null, byte[]>
                                    {
                                        Value = JsonSerializer.SerializeToUtf8Bytes(batch, JsonOptions)
                                    },
                                    timeout.Token);

        await using var connection = new NpgsqlConnection(fixture.PostgreSqlConnectionString);
        await connection.OpenAsync(timeout.Token);
        
        try
        {
            await WaitForProcessedEvent(connection, eventId);
        }
        catch (TimeoutException exception)
        {
            var logs = await fixture.GetTrafficProcessingLogsAsync();

            throw new TimeoutException($"{exception.Message}{Environment.NewLine}{logs}", exception);
        }

        var aggregate = await GetTrafficAggregate(
            connection,
            GetMinuteBucket(occurredAtUtc),
            sourceId,
            destinationIp,
            timeout.Token);

        Assert.Equal(1L, aggregate.PacketCount);
        Assert.Equal(packetLengthBytes, aggregate.TotalBytes);

        var clickHouseCount = await GetClickHouseEventCount(
            eventId,
            batchId,
            timeout.Token);

        Assert.Equal(1L, clickHouseCount);
    }
    
    /// <summary>
    /// Ожидает появления события в таблице обработанных событий PostgreSQL.
    /// </summary>
    private static async Task WaitForProcessedEvent(
        NpgsqlConnection connection,
        Guid eventId)
    {
        const string sql = """
            SELECT EXISTS (
                SELECT 1
                FROM traffic_processing.processed_packet_events
                WHERE event_id = @eventId
            );
            """;

        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("eventId", eventId);

        using var timeout = new CancellationTokenSource(
            TimeSpan.FromSeconds(ProcessingTimeoutSeconds));

        try
        {
            while (true)
            {
                var result = await command.ExecuteScalarAsync(timeout.Token);

                if (result is true)
                {
                    return;
                }

                await Task.Delay(100, timeout.Token);
            }
        }
        catch (OperationCanceledException) when (timeout.IsCancellationRequested)
        {
            throw new TimeoutException(
                $"Событие {eventId} не появилось в PostgreSQL за {ProcessingTimeoutSeconds} секунд.");
        }
    }

    /// <summary>
    /// Получает агрегированные показатели сетевого трафика.
    /// </summary>
    private static async Task<(long PacketCount, long TotalBytes)> GetTrafficAggregate(
        NpgsqlConnection connection,
        DateTimeOffset bucketStartUtc,
        Guid sourceId,
        IPAddress destinationIp,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT packet_count, total_bytes
            FROM traffic_processing.source_destination_traffic_minute
            WHERE bucket_start_utc = @bucketStartUtc
              AND source_id = @sourceId
              AND destination_ip = @destinationIp;
            """;

        await using var command = new NpgsqlCommand(sql, connection);

        command.Parameters.AddWithValue("bucketStartUtc", bucketStartUtc);
        command.Parameters.AddWithValue("sourceId", sourceId);
        command.Parameters.AddWithValue("destinationIp", destinationIp);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        if (!await reader.ReadAsync(cancellationToken))
        {
            throw new InvalidOperationException("Агрегированная запись не найдена.");
        }

        return (reader.GetInt64(0), reader.GetInt64(1));
    }

    /// <summary>
    /// Получает количество сохранённых событий в ClickHouse.
    /// </summary>
    private async Task<long> GetClickHouseEventCount(
        Guid eventId,
        Guid batchId,
        CancellationToken cancellationToken)
    {
        var sql = $"""
                   SELECT count()
                   FROM traffic.packet_events
                   WHERE event_id = toUUID('{eventId}')
                     AND batch_id = toUUID('{batchId}')
                   FORMAT TabSeparated;
                   """;

        using var httpClient = new HttpClient();

        var credentials = Convert.ToBase64String("clickhouse:clickhouse"u8.ToArray());

        httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", credentials); 

        using var content = new StringContent(sql, Encoding.UTF8, "text/plain");
        using var response = await httpClient.PostAsync(
                                                        fixture.ClickHouseHttpEndpoint,
                                                        content,
                                                        cancellationToken);

        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadAsStringAsync(cancellationToken);

        return long.Parse(result.Trim(), CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Округляет время события до начала UTC-минуты.
    /// </summary>
    private static DateTimeOffset GetMinuteBucket(DateTimeOffset timestamp)
    {
        var utc = timestamp.UtcDateTime;

        return new DateTimeOffset(
            utc.Year,
            utc.Month,
            utc.Day,
            utc.Hour,
            utc.Minute,
            0,
            TimeSpan.Zero);
    }
}
