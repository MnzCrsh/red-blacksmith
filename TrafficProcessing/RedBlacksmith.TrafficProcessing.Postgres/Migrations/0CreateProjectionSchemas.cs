using FluentMigrator;

namespace RedBlacksmith.TrafficProcessing.Postgres.Migrations;

/// <inheritdoc/>
[Migration(20261104170000)]
public class CreateProjectionSchemas : Migration
{
    /// <inheritdoc/>
    public override void Up()
    {
        Execute.Sql("CREATE SCHEMA IF NOT EXISTS traffic_processing;");
        Execute.Sql("CREATE SCHEMA IF NOT EXISTS traffic_read;");
    }

    /// <inheritdoc/>
    public override void Down()
    {
        Execute.Sql("DROP SCHEMA IF EXISTS traffic_read CASCADE;");
        Execute.Sql("DROP SCHEMA IF EXISTS traffic_processing CASCADE;");
    }
}
