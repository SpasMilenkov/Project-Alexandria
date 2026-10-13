using AwesomeAssertions;
using Builder.Models;
using Builder.Services;

namespace Builder.Tests.Services;

public sealed class DockerResourceInspectionTests
{
    private const string LinuxInfo = """{"DockerRootDir":"/var/lib/docker","OperatingSystem":"Linux","NCPU":8,"MemTotal":8589934592}""";

    [Fact]
    public void PopulateDockerResources_localLinux_measuresDockerDataFilesystemAndDaemonLimits()
    {
        var resources = new SystemResources { CpuCores = 32, TotalMemoryMb = 32768, AvailableMemoryMb = 32768 };
        string? measuredPath = null;

        SystemChecker.PopulateDockerResources(resources, LinuxInfo, linuxHost: true, path =>
        {
            measuredPath = path;
            return 50L * 1024 * 1024 * 1024;
        });

        measuredPath.Should().Be("/var/lib/docker");
        resources.DockerAvailableDiskMb.Should().Be(51200);
        resources.AvailableDiskMb.Should().Be(51200);
        resources.DockerCpuCores.Should().Be(8);
        resources.CpuCores.Should().Be(8);
        resources.DockerMemoryMb.Should().Be(8192);
        resources.TotalMemoryMb.Should().Be(8192);
        resources.AvailableMemoryMb.Should().Be(0);
    }

    [Fact]
    public void PopulateDockerResources_desktopVm_doesNotSubstituteHostFilesystem()
    {
        var resources = new SystemResources();
        var info = LinuxInfo.Replace("\"Linux\"", "\"Docker Desktop\"");
        var measured = false;

        SystemChecker.PopulateDockerResources(resources, info, linuxHost: true, _ =>
        {
            measured = true;
            return 50_000_000_000;
        });

        measured.Should().BeFalse();
        resources.DockerAvailableDiskMb.Should().BeNull();
        resources.DiskSpaceDetails.Should().Contain("inside a VM");
        resources.TotalMemoryMb.Should().Be(8192);
    }

    [Fact]
    public void PopulateDockerResources_dataPathInaccessible_reportsUnknown()
    {
        var resources = new SystemResources();

        SystemChecker.PopulateDockerResources(resources, LinuxInfo, linuxHost: true, _ => null);

        resources.DockerAvailableDiskMb.Should().BeNull();
        resources.AvailableDiskMb.Should().Be(0);
        resources.DiskSpaceDetails.Should().Contain("/var/lib/docker").And.Contain("not a substitute");
    }

    [Fact]
    public void PopulateDockerResources_invalidDaemonResponse_reportsUnknown()
    {
        var resources = new SystemResources();

        SystemChecker.PopulateDockerResources(resources, "invalid-json", linuxHost: true, _ => 1);

        resources.DockerAvailableDiskMb.Should().BeNull();
        resources.DiskSpaceDetails.Should().Contain("unknown");
    }
}
