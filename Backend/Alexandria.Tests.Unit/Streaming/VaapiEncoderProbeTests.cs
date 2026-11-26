using Alexandria.Common.Processes;
using Alexandria.Services.Streaming.VideoEncoding;
using AwesomeAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace Alexandria.Tests.Unit.Streaming;

public class VaapiEncoderProbeTests
{
    private const string Device = "/dev/dri/renderD128";

    private const string DriverLine =
        "[AVHWDeviceContext @ 0x5e9d218b56c0] VAAPI driver: Mesa Gallium driver 26.1.6 for AMD Radeon Graphics " +
        "(radeonsi, renoir, ACO, DRM 3.64, 7.2.15-generic).";

    [Fact]
    public void Interpret_successfulEncode_isUsableAndNamesTheDriver()
    {
        var stderr = $"[AVHWDeviceContext @ 0x5e9d218b56c0] Initialised VAAPI connection: version 1.20\n{DriverLine}\n";

        var result = VaapiEncoderProbe.Interpret(Device, new ExternalProcessResult(0, "", stderr));

        result.Should().Be(new GpuProbeResult(Device, true,
            "Mesa Gallium driver 26.1.6 for AMD Radeon Graphics (radeonsi, renoir, ACO, DRM 3.64, 7.2.15-generic)", null));
    }

    [Fact]
    public void Interpret_failedEncode_keepsTheLastLinesWithoutLogPrefixes()
    {
        const string stderr =
            "[AVHWDeviceContext @ 0x6405476006c0] No VA display found for device /dev/dri/renderD129.\n" +
            "Device creation failed: -22.\n" +
            "Failed to set value 'vaapi=va:/dev/dri/renderD129' for option 'init_hw_device': Invalid argument\n" +
            "Error parsing global options: Invalid argument\n";

        var result = VaapiEncoderProbe.Interpret(Device, new ExternalProcessResult(234, "", stderr));

        result.Usable.Should().BeFalse();
        result.Driver.Should().BeNull();
        result.Detail.Should().Be(
            "Device creation failed: -22. " +
            "Failed to set value 'vaapi=va:/dev/dri/renderD129' for option 'init_hw_device': Invalid argument " +
            "Error parsing global options: Invalid argument");
    }

    [Fact]
    public void Interpret_failureAfterTheDriverLoaded_stillReportsTheDriver()
    {
        var stderr = $"{DriverLine}\n[h264_vaapi @ 0x5e9d21a85800] No usable encoding entrypoint found for profile VAProfileH264High (7).\n";

        var result = VaapiEncoderProbe.Interpret(Device, new ExternalProcessResult(1, "", stderr));

        result.Usable.Should().BeFalse();
        result.Driver.Should().StartWith("Mesa Gallium driver");
        result.Detail.Should().EndWith("No usable encoding entrypoint found for profile VAProfileH264High (7).");
    }

    [Fact]
    public void Interpret_failureWithoutOutput_reportsTheExitCode()
    {
        var result = VaapiEncoderProbe.Interpret(Device, new ExternalProcessResult(139, "", ""));

        result.Detail.Should().Be("ffmpeg exited with code 139.");
    }

    [Fact]
    public async Task ProbeAsync_missingDevice_isUnusableWithoutRunningFfmpeg()
    {
        var sut = new VaapiEncoderProbe(NullLogger<VaapiEncoderProbe>.Instance);

        var result = await sut.ProbeAsync("/dev/dri/no-such-node", TestContext.Current.CancellationToken);

        result.Should().Be(new GpuProbeResult("/dev/dri/no-such-node", false, null,
            "/dev/dri/no-such-node does not exist in this container."));
    }

    [Fact]
    public async Task ProbeAsync_deviceWithoutPermission_namesTheMissingGroup()
    {
        Assert.SkipUnless(OperatingSystem.IsLinux() && Environment.UserName != "root", "Needs a non-root Linux user.");
        var device = Path.GetTempFileName();

        try
        {
            File.SetUnixFileMode(device, UnixFileMode.None);
            var sut = new VaapiEncoderProbe(NullLogger<VaapiEncoderProbe>.Instance);

            var result = await sut.ProbeAsync(device, TestContext.Current.CancellationToken);

            result.Usable.Should().BeFalse();
            result.Detail.Should().Be($"No permission to open {device}. The container needs the group that owns it on the host " +
                                      "and a device rule that allows it.");
        }
        finally
        {
            File.Delete(device);
        }
    }

    [Fact]
    public void Arguments_encodeOneFrameOnTheGivenDevice()
    {
        var arguments = VaapiEncoderProbe.Arguments(Device);

        arguments.Should().Contain("-init_hw_device \"vaapi=va:/dev/dri/renderD128\"")
            .And.Contain("-frames:v 1").And.Contain("-c:v h264_vaapi").And.EndWith("-f null -");
    }
}
