namespace Builder.SystemCheck;

// A GPU render node the transcoding worker may encode video on. Path is the stable
// /dev/dri/by-path name when udev provides one, since renderD numbers can change between
// boots; DeviceNumber is the node's major:minor, the only device the worker is allowed to open;
// GroupId is the numeric group that owns the node, which the container must join.
public sealed record RenderNode(string Path, string PciAddress, string Vendor, string DeviceNumber, int GroupId)
{
    public string Description => $"{Vendor} GPU at {PciAddress}";
}
