using AwesomeAssertions;
using Builder.Deployment;

namespace Builder.Tests.Deployment;

public sealed class VideoGpuTestReportTests
{
    [Fact]
    public void Parse_workerReport_readsEveryDevice()
    {
        const string output = """
            [{"device":"/dev/dri/by-path/pci-0000:04:00.0-render","usable":true,"driver":"Mesa Gallium driver","detail":null},
             {"device":"/dev/dri/renderD129","usable":false,"driver":null,"detail":"ffmpeg exited with code 1."}]
            """;

        VideoGpuTestReport.Parse(output).Should().Equal(
            new VideoGpuTestResult("/dev/dri/by-path/pci-0000:04:00.0-render", true, "Mesa Gallium driver", null),
            new VideoGpuTestResult("/dev/dri/renderD129", false, null, "ffmpeg exited with code 1."));
    }

    // Docker may print a line of its own before the container's output.
    [Fact]
    public void Parse_textBeforeTheReport_isSkipped()
    {
        VideoGpuTestReport.Parse("Creating network\n[{\"device\":\"/dev/dri/renderD128\",\"usable\":true}]")
            .Should().ContainSingle().Which.Usable.Should().BeTrue();
    }

    [Theory]
    [InlineData("")]
    [InlineData("Error response from daemon")]
    [InlineData("[{\"device\":")]
    [InlineData("{\"device\":\"/dev/dri/renderD128\"}")]
    public void Parse_notAReport_isNull(string output)
    {
        VideoGpuTestReport.Parse(output).Should().BeNull();
    }
}
