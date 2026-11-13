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
            shell.SetMain(Views.ErrorPanel("Installation is not verified", "Required checks have not completed."), []);

            return ScreenOutcome.Abort;
        }

        IRenderable Build()
        {
            var capabilities = ServiceCatalog.Resolve(context.Features);
            var origin = context.Config.PublicEndpoint.Origin;
            var facts = new List<(string Label, string Value)>
            {
                ("Account", context.Config.EnvironmentValues.GetValueOrDefault("ADMIN_EMAIL", context.AdminAccount.Email)),
                ("Sign-in", context.AdminLoginVerified
                    ? "Fresh administrator login verified over HTTPS."
                    : "Existing account preserved; current password and browser sign-in were not verified."),
            };

            if (capabilities.MonitoringEnabled)
            {
                facts.Add(("Monitoring", origin + "/monitoring/"));
            }

            facts.Add(("Settings", context.InstallPath));
            facts.Add(("Installer log", context.DeploymentLogPath ?? "not recorded"));
            facts.Add(("Capabilities", string.Join(", ", FeatureCatalog.All.Where(feature => capabilities.IsEnabled(feature.Id)).Select(feature => feature.Label))));

            return new Rows(
                new Markup($"[bold {Theme.Active.Su}]✓ Alexandria is up and running[/]"),
                Views.Highlight(origin),
                Views.Blank(),
                Views.Facts(facts),
                Views.Blank(),
                Views.WarningPanel("Client trust is not verified. Follow nginx/CLIENT-TRUST.txt in the settings folder."),
                Views.Note("Data uses Docker-managed volumes. Back up settings and data separately."),
                Views.Note("Full details, trust fingerprint and start/stop/repair commands: " + Path.Combine(context.InstallPath, InstallationSummary.FileName)),
                Views.Note("Services keep running after setup closes."));
        }

        while (true)
        {
            shell.SetMain(Build, [new KeyHint("D", "details and commands"), new KeyHint("Enter", "close setup")]);
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
