using Builder.Models;
using Builder.Workflow;

namespace Builder.Services;

public static class InstallationSummary
{
    public const string FileName = "INSTALLATION.txt";

    public static IReadOnlyList<string> Describe(InstallationContext context)
    {
        var config = context.Config;
        var capabilities = ServiceCatalog.Resolve(config.Features);
        var email = config.EnvironmentValues.GetValueOrDefault("ADMIN_EMAIL", context.AdminAccount.Email);
        var lines = new List<string>
        {
            "Alexandria installation",
            "Address: " + config.PublicEndpoint.Origin,
            "Account: " + email,
            context.AdminLoginVerified
                ? "Fresh administrator login and authenticated profile verified over HTTPS."
                : "Existing account preserved. Current password and browser sign-in were not verified; sign in to confirm access.",
            "Client trust: not verified. Follow " + Path.Combine(context.InstallPath, "nginx/CLIENT-TRUST.txt"),
            "Configuration folder: " + context.InstallPath,
            "Environment: " + Path.Combine(context.InstallPath, ".env"),
            "Compose: " + Path.Combine(context.InstallPath, "docker-compose.yml"),
            "Installer log: " + (context.DeploymentLogPath ?? "not recorded"),
            "Source: " + context.SourceRoot,
            "Project: " + config.ProjectName,
            "Data: Docker-managed named volumes, separate from the configuration folder. Back up both settings and data volumes.",
            "Volumes: " + string.Join(", ", capabilities.Volumes.Select(volume => config.VolumeNames.GetValueOrDefault(volume, config.ProjectName + "_" + volume))),
            "Model cache: " + Path.Combine(context.InstallPath, "models"),
            "Enabled capabilities: " + string.Join(", ", FeatureCatalog.All.Where(feature => capabilities.IsEnabled(feature.Id)).Select(feature => feature.Label)),
            "Core: file storage, media metadata and playlist synchronization.",
            "Service checks passed; containers without healthchecks are qualified by process state only.",
        };

        if (capabilities.MonitoringEnabled) lines.Add("Grafana: " + config.PublicEndpoint.Origin + "/monitoring/");

        if (File.Exists(Path.Combine(context.InstallPath, "nginx/certs/server.crt")))
        {
            var certificate = new CertificateService().ReadDetails(context.InstallPath, config.PublicEndpoint.Host, config.Certificates);
            lines.Add("Public trust file: " + certificate.TrustCertificatePath);
            lines.Add("Trust SHA-256: " + certificate.TrustSha256Fingerprint);
        }

        lines.Add("Stop (preserve data): " + InstallationCommands.Compose(config, "stop"));
        lines.Add("Start: " + InstallationCommands.Compose(config, "up -d --no-build --pull never"));
        lines.Add("Status: " + InstallationCommands.Compose(config, "ps --all"));
        lines.Add("Service logs: " + InstallationCommands.Compose(config, "logs -f --tail 100"));
        lines.Add("Repair or change features: run this installer again and select the existing configuration folder: " + context.InstallPath);
        lines.Add("Closing the installer leaves services running.");

        var redactor = new SecretRedactor(() => config.EnvironmentValues);

        return lines.Select(redactor.Redact).ToArray();
    }

    public static void Write(InstallationContext context) => ProtectedFile.WriteAtomic(
        Path.Combine(context.InstallPath, FileName), string.Join(Environment.NewLine, Describe(context)) + Environment.NewLine);
}
