global using Xunit;

[CollectionDefinition("Puzzle API", DisableParallelization = true)]
public sealed class PuzzleApiCollection : ICollectionFixture<Puzzle.IntegrationTests.PuzzleApiFixture> { }
