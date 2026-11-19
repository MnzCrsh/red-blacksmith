using Xunit;

namespace RedBlacksmith.EndToEnd.TrafficProcessing;

[CollectionDefinition("TrafficProcessing", DisableParallelization = true)]
public sealed class TrafficProcessingCollection : ICollectionFixture<TestContainersFixture>;