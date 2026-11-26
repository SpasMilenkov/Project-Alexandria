using AwesomeAssertions;
using Builder.SystemCheck;

namespace Builder.Tests.SystemCheck;

// Builds a small /sys/class/drm and /dev/dri in a temporary folder: each render node links to
// its PCI function folder, which holds the vendor ID, as in sysfs.
public sealed class RenderNodeDetectorTests : IDisposable
{
    private readonly string _root = Directory.CreateTempSubdirectory("render-node-detector-").FullName;

    private string SysClassDrm => Path.Combine(_root, "sys", "class", "drm");

    private string DevDri => Path.Combine(_root, "dev", "dri");

    private void AddNode(string name, string pciAddress, string vendor, bool byPath = true, string? deviceNumber = null)
    {
        var function = Path.Combine(_root, "sys", "devices", pciAddress);
        Directory.CreateDirectory(function);
        File.WriteAllText(Path.Combine(function, "vendor"), vendor + "\n");

        var node = Path.Combine(SysClassDrm, name);
        Directory.CreateDirectory(node);
        Directory.CreateSymbolicLink(Path.Combine(node, "device"), function);
        File.WriteAllText(Path.Combine(node, "dev"), (deviceNumber ?? "226:" + name["renderD".Length..]) + "\n");

        Directory.CreateDirectory(Path.Combine(DevDri, "by-path"));
        File.WriteAllText(Path.Combine(DevDri, name), string.Empty);

        if (byPath)
            File.CreateSymbolicLink(Path.Combine(DevDri, "by-path", $"pci-{pciAddress}-render"), Path.Combine("..", name));
    }

    private IReadOnlyList<RenderNode> Detect(Func<string, int?>? groupOf = null) =>
        RenderNodeDetector.Detect(SysClassDrm, DevDri, groupOf ?? (_ => 110));

    [Fact]
    public void Detect_amdNode_usesItsStableNameAndGroup()
    {
        AddNode("renderD128", "0000:04:00.0", "0x1002");

        Detect().Should().Equal(new RenderNode(
            Path.Combine(DevDri, "by-path", "pci-0000:04:00.0-render"), "0000:04:00.0", "AMD", "226:128", 110));
    }

    // The device number is written into the container's device rule, so only two numbers pass.
    [Theory]
    [InlineData("226:*")]
    [InlineData("226:128 rwm\" ")]
    [InlineData("")]
    public void Detect_malformedDeviceNumber_leavesTheNodeOut(string deviceNumber)
    {
        AddNode("renderD128", "0000:04:00.0", "0x1002", deviceNumber: deviceNumber);

        Detect().Should().BeEmpty();
    }

    [Fact]
    public void Detect_noStableName_fallsBackToTheNodeItself()
    {
        AddNode("renderD128", "0000:00:02.0", "0x8086", byPath: false);

        Detect().Single().Path.Should().Be(Path.Combine(DevDri, "renderD128"));
    }

    [Fact]
    public void Detect_unsupportedVendors_areLeftOut()
    {
        AddNode("renderD128", "0000:01:00.0", "0x10de");
        AddNode("renderD129", "0000:00:05.0", "0x1af4");

        Detect().Should().BeEmpty();
    }

    [Fact]
    public void Detect_severalNodes_listsThemInDeviceOrder()
    {
        AddNode("renderD129", "0000:04:00.0", "0x1002");
        AddNode("renderD128", "0000:00:02.0", "0x8086");

        Detect().Select(node => node.Vendor).Should().Equal("Intel", "AMD");
    }

    [Fact]
    public void Detect_groupUnknown_leavesTheNodeOut()
    {
        AddNode("renderD128", "0000:04:00.0", "0x1002");

        Detect(_ => null).Should().BeEmpty();
    }

    [Fact]
    public void Detect_noDrmFolder_findsNothing()
    {
        RenderNodeDetector.Detect(Path.Combine(_root, "missing"), DevDri, _ => 110).Should().BeEmpty();
    }

    public void Dispose() => Directory.Delete(_root, recursive: true);
}
