using AwesomeAssertions;
using Builder.Services;

namespace Builder.Tests.Integration;

public sealed class GarageCapacityRuntimeTests
{
    [DockerFact]
    [Trait("Category", "Docker")]
    public void Capacity_actual_one_gb_layout_preserves_objects_on_rerun_and_rejects_implicit_resize()
    {
        using var fixture = new CachedInfrastructureFixture();
        fixture.Context.Config.Storage.GarageCapacityGb = 1;
        StorageConfigurationResolver.ApplyEnvironment(fixture.Context.Config.Storage, fixture.Context.Config.EnvironmentValues);
        fixture.Prepare();
        fixture.Start();
        fixture.ReadHandoff();
        var receipt = Path.Combine(fixture.Target, "init-output", "garage-capacity.bytes");
        File.ReadAllText(receipt).Trim().Should().Be("1000000000");
        var originalReceipt = File.ReadAllBytes(receipt);

        RunHelper(fixture, """
            set -eu
            . /output/garage-credentials.env
            export AWS_DEFAULT_REGION=garage AWS_ACCESS_KEY_ID="$GARAGE_S3_ACCESS_KEY" AWS_SECRET_ACCESS_KEY="$GARAGE_S3_SECRET_KEY"
            printf capacity-persistence-fixture > /tmp/capacity-input
            aws --endpoint-url http://garage:3900 s3api put-object --bucket alexandria-files --key capacity-fixture --body /tmp/capacity-input >/dev/null
            """);
        fixture.CommitHandoff();

        fixture.Context.Config.EnvironmentValues["GARAGE_CAPACITY"] = "10G";
        EnvGenerator.WriteFile(Path.Combine(fixture.Target, ".env"), fixture.Context.Config.EnvironmentValues);
        fixture.RepeatInitialization();
        File.ReadAllBytes(receipt).Should().Equal(originalReceipt);
        VerifyObject(fixture);

        var rejected = fixture.Compose(["run", "--rm", "--pull", "never", "--no-deps",
            "--env", "GARAGE_CAPACITY=2G", "--env", "GARAGE_CAPACITY_CHANGE_REQUESTED=true", "garage-init"]);
        rejected.Succeeded.Should().BeFalse();
        rejected.TimedOut.Should().BeFalse();
        rejected.StandardError.Should().Contain("Existing Garage layout capacity is 1000000000 bytes")
            .And.Contain("data were preserved");
        File.ReadAllBytes(receipt).Should().Equal(originalReceipt);
        VerifyObject(fixture);
        fixture.VerifyStorageCredentials();
        TestContext.Current.TestOutputHelper!.WriteLine("Garage v2.2.0: fresh 1 GB layout=1000000000 bytes; stale-default rerun preserved its object; reviewed resize rejected without receipt/object mutation.");
    }

    private static void VerifyObject(CachedInfrastructureFixture fixture) => RunHelper(fixture, """
        set -eu
        . /output/garage-credentials.env
        export AWS_DEFAULT_REGION=garage AWS_ACCESS_KEY_ID="$GARAGE_S3_ACCESS_KEY" AWS_SECRET_ACCESS_KEY="$GARAGE_S3_SECRET_KEY"
        aws --endpoint-url http://garage:3900 s3api get-object --bucket alexandria-files --key capacity-fixture /tmp/capacity-readback >/dev/null
        test "$(cat /tmp/capacity-readback)" = capacity-persistence-fixture
        """);

    private static void RunHelper(CachedInfrastructureFixture fixture, string script)
    {
        var result = fixture.Compose(["run", "--rm", "--pull", "never", "--no-deps", "--entrypoint", "/bin/sh", "garage-init", "-c", script]);
        CachedInfrastructureFixture.Require(result.Succeeded, "The disposable Garage capacity fixture could not preserve its synthetic object.");
    }
}
