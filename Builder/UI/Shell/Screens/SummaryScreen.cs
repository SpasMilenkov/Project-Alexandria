using Builder.Models;
using Builder.Services;
using Builder.Workflow;
using Spectre.Console;
using Spectre.Console.Rendering;

namespace Builder.UI.Shell.Screens;

public static class SummaryScreen
{
    public static ScreenOutcome Run(
        Shell shell,
        IResourceCalculator calculator)
    {
        var context = shell.Context;

        var address = context.Config.PublicEndpoint.Origin;

        // Breakdown is the default: it reads as shares of a whole
        var showBarChart = false;
        var showServices = false;

        IRenderable Build()
        {
            if (showServices)
            {
                return new Rows(
                    Views.Heading("What will run", "Every component this setup starts, with its share of memory."),
                    BuildServicesView(context.Features));
            }

            var chart = showBarChart
                ? Views.MemoryChart(context.Features)
                : Views.MemoryBreakdown(context.Features);

            var rows = new List<IRenderable>
            {
                Views.Heading("Almost done - quick check", "Nothing has been written yet. Enter starts the installation."),
                Views.Section("Your setup"),
                BuildFacts(calculator, context, address),
                Views.Blank(),
                Views.Section($"Memory this will use ({(showBarChart ? "bar" : "breakdown")})"),
                chart,
                Views.Note("Planning estimate including memory headroom; actual use varies with workload."),
                Views.Blank(),
                Views.Section("Storage"),
                new Rows(StorageSummary.Describe(context.Config, context.Resources).Select(Views.Note)),
                Views.Blank(),
                Views.Section("Files that will be created"),
                Views.FileTree(context.Features),
                Views.Blank(),
                Views.Note("Setup preserves established credentials. Changing a stored service password requires that service's credential rotation procedure."),
            };

            if (SourceReadNote(context) is { } note)
            {
                rows.Add(note);
            }

            return new Rows(rows);
        }

        while (true)
        {
            shell.SetMain(Build,
            [
                new KeyHint("Enter", "start installation"),
                new KeyHint("A", showServices ? "back to summary" : "see what will run"),
                new KeyHint("R", $"switch to {(showBarChart ? "breakdown" : "bar chart")}"),
                new KeyHint("Esc", "go back"),
            ], readFromTop: true);

            var key = shell.Keys.Wait();

            if (shell.HandleGlobalKey(key))
            {
                continue;
            }

            switch (key.Key)
            {
                case ConsoleKey.A:
                    showServices = !showServices;
                    break;
                case ConsoleKey.R:
                    showBarChart = !showBarChart;
                    break;
                case ConsoleKey.Enter:
                    return ScreenOutcome.Continue;
                case ConsoleKey.Escape:
                    return ScreenOutcome.Back;
            }
        }
    }

    // When the setup folder differs from the checkout, make the read-only
    // relationship explicit so the model is never a surprise
    private static IRenderable? SourceReadNote(InstallationContext context)
    {
        if (!context.UsingExternalTarget)
        {
            return null;
        }

        return Views.Note(
            $"Images are built from your source code at {context.SourceRoot} - it is only read, never modified.");
    }

    private static IRenderable BuildFacts(
        IResourceCalculator calculator,
        InstallationContext context,
        string address)
    {
        var facts = new List<(string Label, string Value)>
        {
            ("Admin account", context.AdminAccount.Email),
            ("Installation", context.IsRepair ? "Repair existing setup" : "New setup"),
        };

        if (!string.IsNullOrEmpty(context.Config.ProjectName))
        {
            facts.Add(("Docker project", context.Config.ProjectName));
        }

        if (context.Config.PreserveFixedContainerNames)
        {
            facts.Add(("Legacy adoption", "Keep the verified project and existing data volumes"));
            facts.Add(("Data volumes", string.Join("\n", context.Config.VolumeNames.Select(pair => pair.Value))));
        }

        if (context.IsRepair)
        {
            facts.Add(("Current services", context.ExistingStackReady ? "Running and ready" : "Readiness will be checked during repair"));
        }

        var capabilities = ServiceCatalog.Resolve(context.Features);

        facts.Add(("Services", $"{capabilities.RunningServiceCount} ongoing + {capabilities.OneShotServiceIds.Count} setup jobs"));
        facts.Add(("Memory needed", $"about {calculator.CalculateMinimumRamMb(context.Features) / 1024.0:0.#} GB"));

        foreach (var assets in capabilities.ModelAssets)
        {
            var details = $"{capabilities.AudioBackbone}; verified local cache in models; checksums verified again at worker startup";

            if (!string.IsNullOrEmpty(context.SourceRoot) && File.Exists(Path.Combine(context.SourceRoot, "classificators", "essentia", "model-requirements.tsv")))
            {
                var plan = new ModelPreparationService().CreatePlan(context.SourceRoot, context.InstallPath,
                    capabilities.AudioBackbone, context.Config.ModelSourceDirectory);
                details += $"; {plan.ExpectedBytes / (1024.0 * 1024):0.##} MB, {plan.Assets.Count} files";
            }

            facts.Add((assets.Name, details));
        }

        if (capabilities.AudioAnalysisEnabled)
        {
            facts.Add(("Model sourcing", context.Config.AllowModelDownloads
                ? "Reuse verified local files; download missing or corrupt files from essentia.upf.edu (HTTPS)"
                : "Local files only; model downloads disabled"));
            facts.Add(("Audio processing", "One inference job at a time; up to 8 CPU cores and 2 GB worker memory"));
        }

        facts.Add(("Address", address));
        facts.Add(("Certificate", context.Config.Certificates.Mode == CertificateSettings.ImportedMode ? "Import your PEM certificate and key" : "Installation local CA; trust public CA on each client"));
        facts.Add(("Network exposure", "One HTTPS port; previous HTTP and monitoring ports close"));

        return Views.Facts(facts);
    }

    // The final configured picture: every enabled component with a
    // plain-language role and its share of memory
    private static IRenderable BuildServicesView(FeatureSelection features)
    {
        var table = new Table().Border(TableBorder.Simple)
            .BorderStyle(Theme.Active.BorderStyle)
            .AddColumn($"[{Theme.Active.Ac}]Component[/]")
            .AddColumn($"[{Theme.Active.Ac}]What it does[/]")
            .AddColumn(new TableColumn($"[{Theme.Active.Ac}]Memory[/]").RightAligned().NoWrap());

        var capabilities = ServiceCatalog.Resolve(features);

        foreach (var group in capabilities.Services.GroupBy(service => service.Group))
        {
            foreach (var service in group)
            {
                table.AddRow(
                    Markup.Escape(service.Name),
                    Markup.Escape(service.Description),
                    service.OneShot ? Ui.Muted("once, during setup") : $"~{service.MemoryMb} MB");
            }
        }

        table.AddRow(Ui.Muted("Memory headroom"), Ui.Muted("Allowance included in the total planning estimate."),
            $"{capabilities.Services.Where(service => !service.OneShot).Sum(service => service.MemoryHeadroomMb)} MB");

        return table;
    }
}
