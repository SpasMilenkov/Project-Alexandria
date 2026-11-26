using Builder.SystemCheck;

namespace Builder.Diagnostics;

// One render node on this machine: whether the worker image could encode on it (null when it was
// not tested), the driver or the reason, and whether the installation is wired to it.
public sealed record VideoGpuNodeReport(RenderNode Node, bool? Encodes, string? Detail, bool InUse);
