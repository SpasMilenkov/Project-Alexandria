using Builder.Models;

namespace Builder.Services;

public static class InstallationCommands
{
    public static string Compose(InstallationConfig config, string operation) =>
        "docker compose --project-name " + Quote(config.ProjectName) +
        " --project-directory " + Quote(config.InstallPath) +
        " --env-file " + Quote(Path.Combine(config.InstallPath, ".env")) +
        " --file " + Quote(Path.Combine(config.InstallPath, "docker-compose.yml")) + " " + operation;

    private static string Quote(string value) => "'" + value.Replace("'", "'\"'\"'", StringComparison.Ordinal) + "'";
}
