using Xunit;

namespace RedBlacksmith.EndToEnd;

/// <summary>
/// Общая коллекция интеграционных тестов.
/// </summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public class IntegrationTestCollection
{
    /// <summary>
    /// Название коллекции интеграционных тестов.
    /// </summary>
    public const string Name = "Integration";
}
