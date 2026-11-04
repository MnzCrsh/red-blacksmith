using FluentMigrator;

namespace RedBlacksmith.TrafficProcessing.Postgres.Migrations;

/// <summary>
/// Создаёт таблицу обработанных событий для защиты от повторной обработки.
/// </summary>
[Migration(20261104172000)]
public class CreateProcessedPacketEvents : Migration
{
    /// <inheritdoc/>
    public override void Up()
    {
        Execute.Sql("SET ROLE traffic_processing_owner;");

        Execute.Sql("""
                    CREATE TABLE traffic_processing.processed_packet_events
                    (
                        event_id UUID PRIMARY KEY,
                        processed_at_utc TIMESTAMPTZ NOT NULL DEFAULT clock_timestamp()
                    );

                    GRANT SELECT, INSERT, UPDATE, DELETE
                        ON TABLE traffic_processing.processed_packet_events
                        TO traffic_processing_writer;
                    """);

        Execute.Sql("RESET ROLE;");
    }

    /// <inheritdoc/>
    public override void Down()
    {
        Execute.Sql("DROP TABLE IF EXISTS traffic_processing.processed_packet_events;");
    }
}
