using FluentMigrator;

namespace RedBlacksmith.TrafficProcessing.Postgres.Migrations;

/// <summary>
/// Создаёт таблицу минутной агрегации сетевого трафика по IP назначения.
/// </summary>
[Migration(20261104173000)]
public class CreateSourceDestinationTrafficMinute : Migration
{
    /// <inheritdoc/>
    public override void Up()
    {
        Execute.Sql("SET ROLE traffic_processing_owner;");

        Execute.Sql("""
                    CREATE TABLE traffic_processing.source_destination_traffic_minute
                    (
                        bucket_start_utc TIMESTAMPTZ NOT NULL,
                        source_id UUID NOT NULL,
                        destination_ip INET NOT NULL,

                        packet_count BIGINT NOT NULL CHECK (packet_count > 0),
                        total_bytes BIGINT NOT NULL CHECK (total_bytes >= 0),

                        updated_at_utc TIMESTAMPTZ NOT NULL DEFAULT clock_timestamp(),

                        CONSTRAINT pk_source_destination_traffic_minute
                            PRIMARY KEY (bucket_start_utc, source_id, destination_ip)
                    );

                    CREATE INDEX ix_source_destination_traffic_minute_source_bucket
                        ON traffic_processing.source_destination_traffic_minute
                            (source_id, bucket_start_utc DESC);

                    GRANT SELECT, INSERT, UPDATE, DELETE
                        ON TABLE traffic_processing.source_destination_traffic_minute
                        TO traffic_processing_writer;
                    """);

        Execute.Sql("RESET ROLE;");
    }

    /// <inheritdoc/>
    public override void Down()
    {
        Execute.Sql("DROP TABLE IF EXISTS traffic_processing.source_destination_traffic_minute;");
    }
}
