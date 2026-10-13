using Builder.Models;
using Builder.Services;
using Spectre.Console.Rendering;

namespace Builder.UI.Shell.Screens;

public static class SuccessScreen
{
    public static ScreenOutcome Run(Shell shell)
    {
        var context = shell.Context;

        if (!context.DeploymentVerified)
        {
            shell.SetMain(Views.ErrorPanel("Installation is not verified", "Required checks have not completed."));

            return ScreenOutcome.Abort;
        }

        while (true)
        {
            var capabilities = ServiceCatalog.Resolve(context.Features);
            var parts = new List<IRenderable>
            {
                Views.Title("Alexandria is up and running"),
                new Text(context.Config.PublicEndpoint.Origin),
                Views.Note("D opens scrollable details · Enter closes setup; services keep running."),
                Views.Note("Account: " + context.Config.EnvironmentValues.GetValueOrDefault("ADMIN_EMAIL", context.AdminAccount.Email)),
                Views.Note(context.AdminLoginVerified
                    ? "Fresh administrator login verified over HTTPS."
                    : "Existing account preserved; current password and browser sign-in were not verified."),
                Views.Note("Client trust is not verified. Follow nginx/CLIENT-TRUST.txt in the settings folder."),
                Views.Note("Settings: " + context.InstallPath),
                Views.Note("Installer log: " + context.DeploymentLogPath),
                Views.Note("Data uses Docker-managed volumes. Back up settings and data separately."),
                Views.Note("Capabilities: " + string.Join(", ", FeatureCatalog.All.Where(feature => capabilities.IsEnabled(feature.Id)).Select(feature => feature.Label))),
                Views.Note("Full details, trust fingerprint and start/stop/repair commands: " + Path.Combine(context.InstallPath, InstallationSummary.FileName)),
            };

            if (capabilities.MonitoringEnabled) parts.Add(new Text("Grafana: " + context.Config.PublicEndpoint.Origin + "/monitoring/"));

            shell.SetMain(new Rows(parts));
            var key = shell.Keys.Wait();

            if (shell.HandleGlobalKey(key)) continue;
            if (key.Key == ConsoleKey.Enter) break;

            if (key.Key == ConsoleKey.D)
            {
                DeployScreen.Inspect(shell, "Installation details", context.DeploymentLogPath ?? "not recorded", InstallationSummary.Describe(context));
            }
        }

        return ScreenOutcome.Continue;
    }
}
