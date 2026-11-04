using FluentMigrator;

namespace RedBlacksmith.TrafficProcessing.Postgres.Migrations;

/// <summary>
/// Создаёт публичное материализованное представление минутной статистики.
/// </summary>
[Migration(20261104174000)]
public class CreateSourceTrafficMinuteView : Migration 
{
    public override void Up()
    {
        Execute.Sql("SET ROLE traffic_processing_owner;");

        Execute.Sql("""
                    CREATE MATERIALIZED VIEW traffic_read.source_traffic_minute AS
                    SELECT
                        bucket_start_utc,
                        source_id,
                        SUM(packet_count)::BIGINT AS packet_count,
                        SUM(total_bytes)::BIGINT AS total_bytes,
                        COUNT(*)::BIGINT AS unique_destination_count
                    FROM traffic_processing.source_destination_traffic_minute
                    GROUP BY
                        bucket_start_utc,
                        source_id
                    WITH DATA;

                    CREATE UNIQUE INDEX ux_source_traffic_minute_source_bucket
                        ON traffic_read.source_traffic_minute
                            (source_id, bucket_start_utc);

                    GRANT SELECT
                        ON TABLE traffic_read.source_traffic_minute
                        TO traffic_readonly;

                    CREATE FUNCTION traffic_read.refresh_source_traffic_minute()
                    RETURNS VOID
                    LANGUAGE plpgsql
                    SECURITY DEFINER
                    SET search_path = pg_catalog, traffic_read
                    AS $function$
                    BEGIN
                        REFRESH MATERIALIZED VIEW CONCURRENTLY
                            traffic_read.source_traffic_minute;
                    END;
                    $function$;

                    REVOKE ALL
                        ON FUNCTION traffic_read.refresh_source_traffic_minute()
                        FROM PUBLIC;

                    GRANT EXECUTE
                        ON FUNCTION traffic_read.refresh_source_traffic_minute()
                        TO traffic_processing_writer;
                    """);

        Execute.Sql("RESET ROLE;");
    }

    public override void Down()
    {
        Execute.Sql("""
                    DROP FUNCTION IF EXISTS traffic_read.refresh_source_traffic_minute();
                    DROP MATERIALIZED VIEW IF EXISTS traffic_read.source_traffic_minute;
                    """);
    }
}