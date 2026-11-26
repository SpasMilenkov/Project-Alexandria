using System.Globalization;
using System.Text.RegularExpressions;

namespace Builder.SystemCheck;

// Finds the render nodes the worker's VAAPI drivers can use: AMD and Intel only, since the
// worker image ships no NVIDIA encoder. Whether a node really encodes is only known once the
// built worker image has tried it.
public static class RenderNodeDetector
{
    private static readonly Dictionary<string, string> SupportedVendors = new(StringComparer.OrdinalIgnoreCase)
    {
        ["0x1002"] = "AMD",
        ["0x8086"] = "Intel",
    };

    public static IReadOnlyList<RenderNode> Detect(Func<string, int?> groupOf) =>
        Detect("/sys/class/drm", "/dev/dri", groupOf);

    internal static IReadOnlyList<RenderNode> Detect(string sysClassDrm, string devDri, Func<string, int?> groupOf)
    {
        if (!Directory.Exists(sysClassDrm)) return [];

        var nodes = new List<RenderNode>();

        foreach (var entry in Directory.GetFileSystemEntries(sysClassDrm, "renderD*").OrderBy(MinorNumber))
        {
            var device = Path.Combine(entry, "device");
            var vendorFile = Path.Combine(device, "vendor");

            if (!File.Exists(vendorFile) || !SupportedVendors.TryGetValue(File.ReadAllText(vendorFile).Trim(), out var vendor))
                continue;

            // The device link points at the PCI function, whose folder name is its address.
            var pciAddress = Path.GetFileName(Directory.ResolveLinkTarget(device, returnFinalTarget: true)?.FullName ?? device);
            var node = Path.Combine(devDri, Path.GetFileName(entry));
            var byPath = Path.Combine(devDri, "by-path", $"pci-{pciAddress}-render");

            if (DeviceNumber(entry) is not { } deviceNumber || groupOf(node) is not { } groupId) continue;

            nodes.Add(new RenderNode(File.Exists(byPath) ? byPath : node, pciAddress, vendor, deviceNumber, groupId));
        }

        return nodes;
    }

    // The kernel's major:minor for the node. It ends up in the container's device rule, so
    // anything that is not two numbers is refused rather than written into the compose file.
    private static string? DeviceNumber(string entry)
    {
        var file = Path.Combine(entry, "dev");

        if (!File.Exists(file)) return null;

        var value = File.ReadAllText(file).Trim();

        return Regex.IsMatch(value, "^[0-9]+:[0-9]+$") ? value : null;
    }

    private static int MinorNumber(string entry) =>
        int.TryParse(Path.GetFileName(entry)["renderD".Length..], NumberStyles.None, CultureInfo.InvariantCulture, out var minor)
            ? minor
            : int.MaxValue;
}
