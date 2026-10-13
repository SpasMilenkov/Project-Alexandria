using AwesomeAssertions;

namespace Builder.Tests.Integration;

public sealed class ModelRuntimeTests
{
    [DockerFact]
    [Trait("Category", "Docker")]
    public void Inference_both_selected_backbones_verify_subsets_and_share_data_under_actual_worker_uids()
    {
        using var fixture = new CachedModelRuntimeFixture();

        var evidence = fixture.VerifyBothBackbones();

        evidence.Should().Contain("effnet: current inference succeeded with 2 GiB memory cap")
            .And.Contain("maest: current inference succeeded with 2 GiB memory cap")
            .And.Contain("MEMORY_PEAK_BYTES=");
        TestContext.Current.TestOutputHelper!.WriteLine(evidence);
    }
}
