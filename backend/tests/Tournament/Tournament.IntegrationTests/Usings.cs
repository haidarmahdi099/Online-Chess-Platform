global using Xunit;

[CollectionDefinition("Tournament API", DisableParallelization = true)]
public sealed class TournamentApiCollection : ICollectionFixture<global::Tournament.IntegrationTests.TournamentApiFixture> { }
