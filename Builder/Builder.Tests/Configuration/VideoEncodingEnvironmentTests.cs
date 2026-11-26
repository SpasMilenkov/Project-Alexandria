using AwesomeAssertions;
using Builder.Catalog;
using Builder.Configuration;
using Builder.SystemCheck;
using Builder.Workflow;

namespace Builder.Tests.Configuration;

public sealed class VideoEncodingEnvironmentTests
{
    private static readonly RenderNode Amd = new("/dev/dri/by-path/pci-0000:04:00.0-render", "0000:04:00.0", "AMD", "226:128", 110);

    private static ResolvedCapabilities Streaming(bool enabled)
    {
        var features = new FeatureSelection();
        features.SetEnabled(FeatureCatalog.AdaptiveStreaming.Id, enabled);

        return ServiceCatalog.Resolve(features);
    }

    [Fact]
    public void Apply_gpuFound_pointsTheWorkerAtItsStableNameAndGroup()
    {
        var values = new Dictionary<string, string>();

        VideoEncodingEnvironment.Apply(values, Streaming(true), new SystemResources { VideoGpus = [Amd] });

        values.Should().BeEquivalentTo(new Dictionary<string, string>
        {
            [VideoEncodingEnvironment.ModeKey] = "Auto",
            [VideoEncodingEnvironment.DeviceKey] = Amd.Path,
            [VideoEncodingEnvironment.GroupKey] = "110",
            [VideoEncodingEnvironment.DeviceNumberKey] = "226:128",
        });
        VideoEncodingEnvironment.UsesGpu(values).Should().BeTrue();
    }

    // A repair on a machine whose GPU is gone must not keep pointing the worker at it.
    [Fact]
    public void Apply_noGpu_encodesOnTheCpuAndDropsEarlierGpuKeys()
    {
        var values = new Dictionary<string, string>
        {
            [VideoEncodingEnvironment.DeviceKey] = Amd.Path,
            [VideoEncodingEnvironment.GroupKey] = "110",
            [VideoEncodingEnvironment.DeviceNumberKey] = "226:128",
        };

        VideoEncodingEnvironment.Apply(values, Streaming(true), new SystemResources());

        values.Should().BeEquivalentTo(new Dictionary<string, string> { [VideoEncodingEnvironment.ModeKey] = "Cpu" });
        VideoEncodingEnvironment.UsesGpu(values).Should().BeFalse();
    }

    [Fact]
    public void Apply_streamingOff_writesNoVideoKeys()
    {
        var values = new Dictionary<string, string> { [VideoEncodingEnvironment.ModeKey] = "Auto" };

        VideoEncodingEnvironment.Apply(values, Streaming(false), new SystemResources { VideoGpus = [Amd] });

        values.Should().BeEmpty();
    }

    [Fact]
    public void Plan_gpuFound_promisesItOnlyIfItPassesTheTest()
    {
        VideoEncodingEnvironment.Plan(new SystemResources { VideoGpus = [Amd] })
            .Should().Be("On the AMD GPU at 0000:04:00.0, if it passes a test after the build; on the CPU otherwise");
    }

    [Fact]
    public void Plan_detectionFailed_saysWhy()
    {
        VideoEncodingEnvironment.Plan(new SystemResources { VideoGpuDetectionError = "GPUs could not be listed: denied" })
            .Should().Be("On the CPU, because GPUs could not be listed: denied");
    }

    [Fact]
    public void Plan_gpuUnavailable_saysWhyInsteadOfNoneFound()
    {
        VideoEncodingEnvironment.Plan(new SystemResources { VideoGpuUnavailableReason = "no detected GPU could encode in the test after the build" })
            .Should().Be("On the CPU, because no detected GPU could encode in the test after the build");
    }

    [Fact]
    public void Outcome_streamingOff_isNotReported()
    {
        var context = new InstallationContext();
        context.Features.SetEnabled(FeatureCatalog.AdaptiveStreaming.Id, false);

        VideoEncodingEnvironment.Outcome(context).Should().BeNull();
    }

    [Fact]
    public void Outcome_noGpuFound_namesTheCpu()
    {
        var context = new InstallationContext();
        context.Features.SetEnabled(FeatureCatalog.AdaptiveStreaming.Id, true);

        VideoEncodingEnvironment.Outcome(context).Should().Be("On the CPU, because no AMD or Intel GPU was found");
    }
}
