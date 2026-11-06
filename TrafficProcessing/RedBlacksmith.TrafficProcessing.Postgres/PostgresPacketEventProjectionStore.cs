using Dapper;
using Npgsql;
using NpgsqlTypes;
using RedBlacksmith.TrafficProcessing.Models.Abstracts;
using RedBlacksmith.TrafficProcessing.Models.DomainModels;

namespace RedBlacksmith.TrafficProcessing.Postgres;

/// <inheritdoc/>
public class PostgresPacketEventProjectionStore(NpgsqlDataSource dataSource) : IPacketEventProjectionStore
{
    /// <summary>
    /// Запрос для создания временной буферной таблицы для подготовки данных.
    /// </summary>
    private const string CreateStageTableSql = """
                                               CREATE TEMP TABLE packet_event_stage
                                               (
                                                   event_id UUID PRIMARY KEY,
                                                   source_id UUID NOT NULL,
                                                   occurred_at_utc TIMESTAMPTZ NOT NULL,
                                                   destination_ip INET NOT NULL,
                                                   packet_length_bytes BIGINT NOT NULL
                                               ) ON COMMIT DROP;
                                               """;
    
    /// <summary>
    /// Запрос для массового копирования данных из бинарного формата.
    /// </summary>
    private const string CopyStageSql = """
                                        COPY pg_temp.packet_event_stage
                                        (
                                            event_id,
                                            source_id,
                                            occurred_at_utc,
                                            destination_ip,
                                            packet_length_bytes
                                        )
                                        FROM STDIN (FORMAT BINARY);
                                        """;
    
    /// <summary>
    /// Запрос для создания проекции данных.
    /// </summary>
    private const string ApplyProjectionSql = """
                                              WITH inserted_events AS
                                              (
                                                  INSERT INTO traffic_processing.processed_packet_events
                                                  (
                                                      event_id
                                                  )
                                                  SELECT event_id
                                                  FROM pg_temp.packet_event_stage
                                                  ON CONFLICT (event_id) DO NOTHING
                                                  RETURNING event_id
                                              ),
                                              aggregated_events AS
                                              (
                                                  SELECT
                                                      date_trunc(
                                                          'minute', stage.occurred_at_utc AT TIME ZONE 'UTC'
                                                      ) AT TIME ZONE 'UTC' AS bucket_start_utc,

                                                      stage.source_id,
                                                      stage.destination_ip,

                                                      COUNT(*)::BIGINT AS packet_count,
                                                      SUM(stage.packet_length_bytes)::BIGINT AS total_bytes

                                                  FROM pg_temp.packet_event_stage AS stage
                                                  INNER JOIN inserted_events AS inserted
                                                      ON inserted.event_id = stage.event_id

                                                  GROUP BY
                                                      1,
                                                      2,
                                                      3
                                              )
                                              INSERT INTO traffic_processing.source_destination_traffic_minute AS target
                                              (
                                                  bucket_start_utc,
                                                  source_id,
                                                  destination_ip,
                                                  packet_count,
                                                  total_bytes
                                              )
                                              SELECT
                                                  bucket_start_utc,
                                                  source_id,
                                                  destination_ip,
                                                  packet_count,
                                                  total_bytes
                                              FROM aggregated_events
                                              ON CONFLICT
                                              (
                                                  bucket_start_utc,
                                                  source_id,
                                                  destination_ip
                                              )
                                              DO UPDATE SET
                                                  packet_count = target.packet_count + EXCLUDED.packet_count,
                                                  total_bytes = target.total_bytes + EXCLUDED.total_bytes,
                                                  updated_at_utc = clock_timestamp();
                                              """;
    
    /// <inheritdoc/>
    public async Task ApplyAsync(IReadOnlyCollection<PacketEvent> events, CancellationToken cancellationToken)
    {
        if (events.Count is 0)
            return;
        
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        
        await BuildProjections(events, connection, transaction, cancellationToken);

        await transaction.CommitAsync(cancellationToken);
    }

    private static async Task BuildProjections(IReadOnlyCollection<PacketEvent> events,
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        CancellationToken cancellationToken)
    {
        await using (var importer = await connection.BeginBinaryImportAsync(CopyStageSql, cancellationToken))
        {
            await connection.ExecuteAsync(new CommandDefinition(CreateStageTableSql, transaction, cancellationToken: cancellationToken));
            
            foreach (var packetEvent in events)
            {
                await MapBinaryRecords(importer, packetEvent, cancellationToken);
            }

            await importer.CompleteAsync(cancellationToken);
        }

        await connection.ExecuteAsync(new CommandDefinition(ApplyProjectionSql, transaction, cancellationToken: cancellationToken));
    }

    private static async Task MapBinaryRecords(NpgsqlBinaryImporter importer, PacketEvent packetEvent, CancellationToken cancellationToken)
    {
        await importer.StartRowAsync(cancellationToken);
        
        await importer.WriteAsync(packetEvent.EventId, NpgsqlDbType.Uuid, cancellationToken);
        
        await importer.WriteAsync(packetEvent.SourceId, NpgsqlDbType.Uuid, cancellationToken);
        
        await importer.WriteAsync(packetEvent.OccurredAtUtc.UtcDateTime, NpgsqlDbType.TimestampTz, cancellationToken);
        
        await importer.WriteAsync(packetEvent.DestinationIp, NpgsqlDbType.Inet, cancellationToken);
        
        await importer.WriteAsync((long)packetEvent.PacketLengthBytes, NpgsqlDbType.Bigint, cancellationToken);
    }
}