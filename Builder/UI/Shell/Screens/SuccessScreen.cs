using Builder.Catalog;
using Builder.Certificates;
using Builder.Configuration;
using Builder.UI.Shell.Screens.Components;
using Builder.UI.Shell.Splash;
using Builder.UI.Theming;
using Builder.Workflow;
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

            // The city from the opening splash, now with every block built
            return new Rows(
                new CityScene { Height = DeployScreen.CityStripRows, Complete = true },
                Views.Blank(),
                new Markup($"[bold {Theme.Active.Su}]✓ Alexandria is up and running[/]"),
                Views.Highlight(origin),
                Views.Blank(),
                Views.Facts(facts),
                Views.Blank(),
                TrustNote(context),
                Views.Note("Data uses Docker-managed volumes. Back up settings and data separately."),
                Views.Note("Full details, trust fingerprint and start/stop/repair commands: " + Path.Combine(context.InstallPath, InstallationSummary.FileName)),
                Views.Note("Services keep running after setup closes."));
        }

        while (true)
        {
            shell.SetMain(Build, [new KeyHint("D", "details and commands"), new KeyHint("Enter", "close setup")], readFromTop: true);
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

    // Trusting the certificate is a normal step after a successful install,
    // not a failure, so it reads as the next thing to do rather than a warning
    private static IRenderable TrustNote(InstallationContext context)
    {
        var steps = Markup.Escape(Path.Combine(context.InstallPath, "nginx", "CLIENT-TRUST.txt"));

        if (context.Config.Certificates.Mode == CertificateSettings.ImportedMode)
        {
            return Views.InfoPanel("Using your certificate",
                $"If a browser warns about the address, the steps are in {steps}");
        }

        return Views.InfoPanel("Next: trust this address on your devices",
            $"Setup made its own certificate, so browsers warn until each device trusts it once. The steps are in {steps}");
    }
}
