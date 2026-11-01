using Alexandria.Common.Config;
using NetVips;

namespace Alexandria.Services.Preview.Images;

/// <summary>
/// Process-wide libvips hardening. libvips keeps this state globally, so it is applied once.
/// </summary>
public static class VipsRuntime
{
    // Abstract loader classes: unblocking one re-enables its file, buffer and source variants.
    internal static readonly string[] AllowedLoaders =
    [
        "VipsForeignLoadJpeg",
        "VipsForeignLoadUhdr",
        "VipsForeignLoadPng",
        "VipsForeignLoadWebp",
        "VipsForeignLoadNsgif",
        "VipsForeignLoadHeif",
        "VipsForeignLoadTiff",
        "VipsForeignLoadSvg"
    ];

    private static readonly Lock Gate = new();
    private static bool _configured;

    public static string Version =>
        $"{NetVips.NetVips.Version(0)}.{NetVips.NetVips.Version(1)}.{NetVips.NetVips.Version(2)}";

    public static void EnsureConfigured(ImagePreviewConfig config)
    {
        lock (Gate)
        {
            if (_configured)
                return;

            NetVips.NetVips.BlockUntrusted = true;

            Operation.Block("VipsForeignLoad", true);
            foreach (var loader in AllowedLoaders)
                Operation.Block(loader, false);

            // The operation cache only pays off for repeated work on the same image, which a
            // preview worker never does; it would just pin decoded pixels between jobs.
            Cache.Max = 0;
            Cache.MaxFiles = 0;
            Cache.MaxMem = 0;

            if (config.Concurrency > 0)
                NetVips.NetVips.Concurrency = config.Concurrency;

            _configured = true;
        }
    }
}
