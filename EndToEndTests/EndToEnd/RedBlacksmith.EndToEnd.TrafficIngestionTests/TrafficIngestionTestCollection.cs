using Xunit;

namespace RedBlacksmith.EndToEnd.TrafficIngestionTests;

[CollectionDefinition("TrafficIngestion", DisableParallelization = true)]
public sealed class TrafficIngestionTestCollection : ICollectionFixture<TestContainersFixture>;