using Builder.Configuration;

namespace Builder.Workflow;

public static class InstallerArguments
{
    public const string Usage = "Usage: Builder [--settings <installation-settings-folder>]\n" +
                                "       Builder --diagnose gpu [--settings <installation-settings-folder>]\n" +
                                "Select an existing settings folder to review and repair the same installation.\n" +
                                "--diagnose gpu tests which GPUs the transcoding worker can encode video on,\n" +
                                "building a temporary worker image from source when none is installed.";

    public static bool Apply(string[] arguments, InstallationContext context)
    {
        if (arguments.Length == 0) return true;
        if (arguments is ["--help"] or ["-h"]) return false;

        if (arguments is ["--diagnose", "gpu", .. var options])
        {
            context.InstallPath = options switch
            {
                [] => DefaultSettingsFolder(context),
                ["--settings", var folder] => Resolve(folder, context),
                _ => throw new ArgumentException(Usage),
            };
            context.DiagnoseGpu = true;

            return true;
        }

        if (arguments is not ["--settings", var path])
        {
            throw new ArgumentException(Usage);
        }

        context.InstallPath = Resolve(path, context);
        var inspection = InstallationConfigurationResolver.Inspect(context);

        if (inspection.SavedConfiguration is { } saved)
        {
            context.SourceRoot = saved.SourceRoot;
        }

        return true;
    }

    // The launch folder when it holds an installation, otherwise the checkout setup runs from,
    // which is where an installation lives unless another folder was chosen.
    public static string DefaultSettingsFolder(InstallationContext context)
    {
        if (File.Exists(Path.Combine(context.LaunchDirectory, "alexandria-config.json"))) return context.LaunchDirectory;

        return PathResolver.FindSourceRoot(context.LaunchDirectory) ?? context.LaunchDirectory;
    }

    private static string Resolve(string path, InstallationContext context)
    {
        if (!PathResolver.TryResolve(path, context.LaunchDirectory, out var target, out var error))
        {
            throw new ArgumentException(error);
        }

        return target;
    }
}
