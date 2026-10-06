using Builder.Services;

namespace Builder.Workflow;

public static class InstallerArguments
{
    public const string Usage = "Usage: Builder [--settings <installation-settings-folder>]\n" +
                                "Select an existing settings folder to review and repair the same installation.";

    public static bool Apply(string[] arguments, InstallationContext context)
    {
        if (arguments.Length == 0) return true;
        if (arguments is ["--help"] or ["-h"]) return false;

        if (arguments is not ["--settings", var path])
        {
            throw new ArgumentException(Usage);
        }

        if (!PathResolver.TryResolve(path, context.LaunchDirectory, out var target, out var error))
        {
            throw new ArgumentException(error);
        }

        context.InstallPath = target;
        var inspection = InstallationConfigurationResolver.Inspect(context);

        if (inspection.SavedConfiguration is { } saved)
        {
            context.SourceRoot = saved.SourceRoot;
        }

        return true;
    }
}
