namespace PremierLeaguePredictions.Tests.Integration;

/// <summary>
/// Defines a test collection to ensure integration tests run sequentially
/// and share the same TestWebApplicationFactory instance.
/// </summary>
[CollectionDefinition("Integration Tests")]
public class IntegrationTestCollection : ICollectionFixture<TestWebApplicationFactory>
{
    // This class is intentionally empty.
    // It's used only to define the test collection and apply the fixture.
}
