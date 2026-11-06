using FluentMigrator;

namespace RedBlacksmith.TrafficProcessing.Postgres.Migrations;

/// <summary>
/// Создаёт роли доступа к PostgreSQL.
/// </summary>
[Migration(20261104171000)]
public class CreatePostgreSqlRoles : Migration
{
    /// <inheritdoc/>
    public override void Up()
    {
        Execute.Sql("""
            DO $$
            BEGIN
                IF NOT EXISTS (
                    SELECT 1 FROM pg_roles WHERE rolname = 'traffic_processing_owner'
                ) THEN
                    EXECUTE 'CREATE ROLE traffic_processing_owner NOLOGIN';
                END IF;

                IF NOT EXISTS (
                    SELECT 1 FROM pg_roles WHERE rolname = 'traffic_processing_writer'
                ) THEN
                    EXECUTE 'CREATE ROLE traffic_processing_writer NOLOGIN';
                END IF;

                IF NOT EXISTS (
                    SELECT 1 FROM pg_roles WHERE rolname = 'traffic_readonly'
                ) THEN
                    EXECUTE 'CREATE ROLE traffic_readonly NOLOGIN';
                END IF;

                IF NOT EXISTS (
                    SELECT 1 FROM pg_roles WHERE rolname = 'traffic_processing'
                ) THEN
                    EXECUTE 'CREATE ROLE traffic_processing LOGIN INHERIT';
                END IF;

                IF NOT EXISTS (
                    SELECT 1 FROM pg_roles WHERE rolname = 'traffic_api'
                ) THEN
                    EXECUTE 'CREATE ROLE traffic_api LOGIN INHERIT';
                END IF;
            END
            $$;
            """);

        Execute.Sql("""
            GRANT traffic_processing_writer TO traffic_processing;
            GRANT traffic_readonly TO traffic_api;

            ALTER SCHEMA traffic_processing OWNER TO traffic_processing_owner;
            ALTER SCHEMA traffic_read OWNER TO traffic_processing_owner;

            GRANT USAGE ON SCHEMA traffic_processing TO traffic_processing_writer;
            GRANT USAGE ON SCHEMA traffic_read TO traffic_readonly;

            ALTER DEFAULT PRIVILEGES FOR ROLE traffic_processing_owner
                IN SCHEMA traffic_processing
                GRANT SELECT, INSERT, UPDATE, DELETE ON TABLES
                TO traffic_processing_writer;

            ALTER DEFAULT PRIVILEGES FOR ROLE traffic_processing_owner
                IN SCHEMA traffic_read
                GRANT SELECT ON TABLES
                TO traffic_readonly;
            """);
    }

    /// <inheritdoc/>
    public override void Down()
    {
        Execute.Sql("""
                    REVOKE traffic_processing_writer FROM traffic_processing;
                    REVOKE traffic_readonly FROM traffic_api;

                    REVOKE USAGE ON SCHEMA traffic_processing FROM traffic_processing_writer;
                    REVOKE USAGE ON SCHEMA traffic_read FROM traffic_readonly;

                    ALTER DEFAULT PRIVILEGES FOR ROLE traffic_processing_owner
                        IN SCHEMA traffic_processing
                        REVOKE SELECT, INSERT, UPDATE, DELETE ON TABLES
                        FROM traffic_processing_writer;

                    ALTER DEFAULT PRIVILEGES FOR ROLE traffic_processing_owner
                        IN SCHEMA traffic_read
                        REVOKE SELECT ON TABLES
                        FROM traffic_readonly;

                    ALTER SCHEMA traffic_processing OWNER TO CURRENT_USER;
                    ALTER SCHEMA traffic_read OWNER TO CURRENT_USER;

                    DROP ROLE IF EXISTS traffic_processing;
                    DROP ROLE IF EXISTS traffic_api;
                    DROP ROLE IF EXISTS traffic_processing_writer;
                    DROP ROLE IF EXISTS traffic_readonly;
                    DROP ROLE IF EXISTS traffic_processing_owner;
                    """);
    }
}
