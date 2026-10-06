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
        ICredentialService credentials,
        IResourceCalculator calculator)
    {
        var context = shell.Context;

        context.Config.Credentials = credentials.GenerateAllCredentials(context.Features);

        var ramGb = calculator.CalculateMinimumRamMb(context.Features) / 1024.0;
        var httpPort = context.Config.Ports.GetValueOrDefault("HTTP_PORT", 80);
        var address = httpPort == 80 ? "http://localhost" : $"http://localhost:{httpPort}";

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
                    Views.Note("Rough estimate of steady-state memory for the selected features."),
                    Views.Title("Files that will be created"),
                    new Padder(Views.FileTree(context.Features), new Padding(0, 0, 2, 1)),
                    Views.Note("Passwords are created automatically. You can change them later in the .env file."),
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
        if (string.IsNullOrWhiteSpace(context.SourceRoot) ||
            string.Equals(
                Path.GetFullPath(context.InstallPath).TrimEnd(Path.DirectorySeparatorChar),
                Path.GetFullPath(context.SourceRoot).TrimEnd(Path.DirectorySeparatorChar),
                StringComparison.OrdinalIgnoreCase))
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
        configTable.AddRow("Services", $"{calculator.CountServices(context.Features)} will run");
        configTable.AddRow("Memory needed", $"about {calculator.CalculateMinimumRamMb(context.Features) / 1024.0:0.#} GB");
        configTable.AddRow("Address", address);

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

        foreach (var group in ServiceCatalog.All.GroupBy(s => s.Group))
        {
            var enabled = group.Where(s => s.IsEnabled(features)).ToList();

            if (enabled.Count == 0)
            {
                continue;
            }

            foreach (var service in enabled)
            {
                table.AddRow(
                    Markup.Escape(service.Name),
                    Markup.Escape(service.Description),
                    service.OneShot ? "[dim]once, during setup[/]" : $"~{service.MemoryGb:0.#} GB");
            }
        }

        return table;
    }
}
