global using Xunit;

[CollectionDefinition("Analysis API", DisableParallelization = true)]
public sealed class AnalysisApiCollection : ICollectionFixture<global::Analysis.IntegrationTests.AnalysisApiFixture> { }
