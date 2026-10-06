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

        // D49: breakdown is the default - it reads as shares of a whole
        var showBarChart = false;
        var showServices = false;

        IRenderable Build()
        {
            var chart = showBarChart
                ? Views.MemoryChart(context.Features)
                : Views.MemoryBreakdown(context.Features);

            var chartCaption = $"Memory this will use [dim]({(showBarChart ? "bar" : "breakdown")})[/]";

            IRenderable content = showServices
                ? BuildServicesView(context.Features)
                : new Rows(
                    new Padder(BuildConfigTable(calculator, context, address), new Padding(0, 0, 2, 1)),
                    Views.Title(chartCaption),
                    new Padder(chart, new Padding(0, 0, 2, 1)),
                    Views.Note("Planning estimate including memory headroom; actual use varies with workload."),
                    new Rows(StorageSummary.Describe(context.Config, context.Resources).Select(Views.Note)),
                    Views.Title("Files that will be created"),
                    new Padder(Views.FileTree(context.Features), new Padding(0, 0, 2, 1)),
                    Views.Note("Setup preserves established credentials. Changing a stored service password requires that service's credential rotation procedure."),
                    SourceReadNote(context) ?? new Markup(string.Empty));

            return new Rows(
                Views.Title("Almost done - quick check"),
                content,
                new Rule().RuleStyle(Theme.Active.BorderStyle),
                new Markup($"  [{Theme.Active.Ac}]A[/] [dim]{(showServices ? "back to summary" : "see what will run")}[/]   " +
                           $"[{Theme.Active.Ac}]R[/] [dim]switch to {(showBarChart ? "breakdown" : "bar chart")}[/]   " +
                           $"[{Theme.Active.Ac}]Enter[/] [dim]start installation[/]   " +
                           $"[{Theme.Active.Ac}]Esc[/] [dim]go back[/]"));
        }

        while (true)
        {
            shell.SetMain(Build());

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
                    shell.SignalEscape();

                    if (shell.TakeEscape())
                    {
                        return ScreenOutcome.Back;
                    }

                    break;
            }
        }
    }

    // When the setup folder differs from the checkout, make the read-only
    // relationship explicit so the model is never a surprise (F9a)
    private static IRenderable? SourceReadNote(InstallationContext context)
    {
        if (!context.UsingExternalTarget)
        {
            return null;
        }

        return Views.Note(
            $"Images are built from your source code at {context.SourceRoot} - it is only read, never modified.");
    }

    private static IRenderable BuildConfigTable(
        IResourceCalculator calculator,
        InstallationContext context,
        string address)
    {
        var configTable = new Table().Border(TableBorder.Simple)
            .BorderStyle(Theme.Active.BorderStyle)
            .AddColumn("[bold]Your setup[/]")
            .AddColumn("");

        configTable.AddRow("Admin account", Markup.Escape(context.AdminAccount.Email));
        configTable.AddRow("Installation", context.IsRepair ? "Repair existing setup" : "New setup");

        if (!string.IsNullOrEmpty(context.Config.ProjectName))
        {
            configTable.AddRow("Docker project", Markup.Escape(context.Config.ProjectName));
        }

        if (context.Config.PreserveFixedContainerNames)
        {
            configTable.AddRow("Legacy adoption", "Keep the verified project and existing data volumes");
            configTable.AddRow("Data volumes", string.Join("\n", context.Config.VolumeNames.Select(pair => Markup.Escape(pair.Value))));
        }

        if (context.IsRepair)
        {
            configTable.AddRow("Current services", context.ExistingStackReady ? "Running and ready" : "Readiness will be checked during repair");
        }

        var capabilities = ServiceCatalog.Resolve(context.Features);

        configTable.AddRow("Services", $"{capabilities.RunningServiceCount} ongoing + {capabilities.OneShotServiceIds.Count} setup jobs");
        configTable.AddRow("Memory needed", $"about {calculator.CalculateMinimumRamMb(context.Features) / 1024.0:0.#} GB");

        foreach (var assets in capabilities.ModelAssets)
        {
            var details = $"{capabilities.AudioBackbone}; verified local cache in models; checksums verified again at worker startup";

            if (!string.IsNullOrEmpty(context.SourceRoot) && File.Exists(Path.Combine(context.SourceRoot, "classificators", "essentia", "model-requirements.tsv")))
            {
                var plan = new ModelPreparationService().CreatePlan(context.SourceRoot, context.InstallPath,
                    capabilities.AudioBackbone, context.Config.ModelSourceDirectory);
                details += $"; {plan.ExpectedBytes / (1024.0 * 1024):0.##} MB, {plan.Assets.Count} files";
            }

            configTable.AddRow(assets.Name, Markup.Escape(details));
        }
        if (capabilities.AudioAnalysisEnabled)
        {
            configTable.AddRow("Model sourcing", context.Config.AllowModelDownloads
                ? "Reuse verified local files; download missing or corrupt files from essentia.upf.edu (HTTPS)"
                : "Existing local files only; model downloads disabled");
            configTable.AddRow("Audio processing", "One inference job at a time; up to 8 CPU cores and 2 GB worker memory");
        }

        configTable.AddRow("Address", address);
        configTable.AddRow("Certificate", context.Config.Certificates.Mode == CertificateSettings.ImportedMode ? "Import your PEM certificate and key" : "Installation local CA; trust public CA on each client");
        configTable.AddRow("Network exposure", "One HTTPS port; previous HTTP and monitoring ports close");

        return configTable;
    }

    // D50: the final configured picture - every enabled component with a
    // plain-language role and its share of memory
    private static IRenderable BuildServicesView(FeatureSelection features)
    {
        var table = new Table().Border(TableBorder.Rounded)
            .BorderStyle(Theme.Active.BorderStyle)
            .AddColumn("[bold]Component[/]")
            .AddColumn("[bold]What it does[/]")
            .AddColumn("[bold]Memory[/]");

        var capabilities = ServiceCatalog.Resolve(features);

        foreach (var group in capabilities.Services.GroupBy(service => service.Group))
        {
            foreach (var service in group)
            {
                table.AddRow(
                    Markup.Escape(service.Name),
                    Markup.Escape(service.Description),
                    service.OneShot ? "[dim]once, during setup[/]" : $"~{service.MemoryMb} MB");
            }
        }

        table.AddRow("Memory headroom", "Allowance included in the total planning estimate.",
            $"{capabilities.Services.Where(service => !service.OneShot).Sum(service => service.MemoryHeadroomMb)} MB");

        return table;
    }
}
