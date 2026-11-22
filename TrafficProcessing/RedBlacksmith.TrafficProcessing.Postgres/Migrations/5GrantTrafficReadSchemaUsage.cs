using FluentMigrator;

namespace RedBlacksmith.TrafficProcessing.Postgres.Migrations;

/// <summary>
/// Выдаёт runtime-роли право обращаться к схеме аналитических представлений.
/// </summary>
[Migration(202611220001)]
public class GrantTrafficReadSchemaUsage : Migration
{
    /// <summary>
    /// Выдаёт право использования схемы traffic_read.
    /// </summary>
    public override void Up()
    {
        Execute.Sql("GRANT USAGE ON SCHEMA traffic_read TO traffic_processing;");
    }

    /// <summary>
    /// Отзывает право использования схемы traffic_read.
    /// </summary>
    public override void Down()
    {
        Execute.Sql("REVOKE USAGE ON SCHEMA traffic_read FROM traffic_processing;");
    }
}