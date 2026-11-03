namespace RedBlacksmith.TrafficProcessing.Db.Models;

/// <summary>
/// Настройки подключения к ClickHouse.
/// </summary>
internal sealed record ClickHouseOptions(string ConnectionString, string Database);