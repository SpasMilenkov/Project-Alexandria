using AwesomeAssertions;

namespace Builder.Tests.Integration;

public sealed class InferenceBrokerTests
{
    [DockerFact]
    [Trait("Category", "Docker")]
    public void Effnet_real_coordinator_dispatches_classifies_persists_and_cleans_shared_files() => Verify("effnet");

    [DockerFact]
    [Trait("Category", "Docker")]
    public void Maest_real_coordinator_dispatches_classifies_persists_and_cleans_shared_files() => Verify("maest");

    private static void Verify(string backbone)
    {
        using var fixture = new CachedInferenceBrokerFixture(backbone);
        fixture.PrepareAndStart();

        var evidence = fixture.VerifyChain();

        evidence.Backbone.Should().Be(backbone);
        evidence.GenrePredictions.Should().Be(5);
        evidence.MoodHeads.Should().Be(7);
        evidence.GenreVersion.Should().Contain(backbone);
        evidence.MoodVersion.Should().Contain("discogs-effnet");
        evidence.MemoryPeakBytes.Should().BeGreaterThan(0).And.BeLessThanOrEqualTo(2L * 1024 * 1024 * 1024);
        TestContext.Current.TestOutputHelper!.WriteLine($"{backbone}: coordinator/broker/inference/database/cleanup verified; peak {evidence.MemoryPeakBytes} bytes.");
    }
}
