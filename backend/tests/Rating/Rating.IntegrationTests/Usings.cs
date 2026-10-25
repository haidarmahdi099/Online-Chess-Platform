global using Xunit;

[CollectionDefinition("Rating API", DisableParallelization = true)]
public sealed class RatingApiCollection : ICollectionFixture<global::Rating.IntegrationTests.RatingApiFixture> { }
