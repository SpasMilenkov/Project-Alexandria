using Builder.Models;

namespace Builder.Workflow;

public static class SourceAssetValidator
{
    public static string? Validate(string sourceRoot, FeatureSelection features, string? templateRoot = null)
    {
        templateRoot ??= Path.Combine(AppContext.BaseDirectory, "Templates", "Local");

        try
        {
            if (!Directory.Exists(sourceRoot))
            {
                return $"Source folder '{sourceRoot}' does not exist or is not a directory.";
            }

            var capabilities = ServiceCatalog.Resolve(features);

            foreach (var relativePath in capabilities.RequiredSourceFiles)
            {
                var error = ValidateReadableFile(Path.Combine(sourceRoot, relativePath), "source asset");

                if (error is not null)
                {
                    return error;
                }
            }

            foreach (var relativePath in capabilities.RequiredTemplates)
            {
                var error = ValidateReadableFile(Path.Combine(templateRoot, relativePath), "installer template");

                if (error is not null)
                {
                    return error;
                }
            }

            return null;
        }
        catch (Exception exception) when (exception is ArgumentException or IOException or UnauthorizedAccessException or NotSupportedException)
        {
            return $"Cannot read source assets: {exception.Message}";
        }
    }

    private static string? ValidateReadableFile(string path, string description)
    {
        if (!File.Exists(path))
        {
            return $"Missing required {description}: {path}";
        }

        using var file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);

        return null;
    }
}
