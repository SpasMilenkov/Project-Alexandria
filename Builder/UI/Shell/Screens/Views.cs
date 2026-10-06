using Builder.Models;
using Builder.Services;
using Builder.Workflow;
using Spectre.Console;
using Spectre.Console.Rendering;

namespace Builder.UI.Shell.Screens;

// Renderable builders shared across screens. Everything that used to write
// straight to the console now returns renderables so the frame stays intact.
public static class Views
{
    public static IRenderable Banner()
    {
        return new Rows(
            new FigletText("ALEXANDRIA").Color(Theme.Active.Accent),
            new Rule($"[{Theme.Active.As}]{Theme.Active.DisplayName} look[/]  " +
                     Ui.Dim($"installer v0.4 - {Theme.Active.Tagline}"))
                .RuleStyle(Theme.Active.BorderStyle));
    }

    public static IRenderable Title(string text)
    {
        return new Markup($"[bold white]{Markup.Escape(text)}[/]");
    }

    private const string SpinnerGlyphs = "⠋⠙⠹⠸⠼⠴⠦⠧⠇⠏";

    // Wall-clock derived so animation survives even when no screen code runs
    // (Live repaints from the frame timer, nothing needs to count ticks)
    public static string SpinnerFrame()
    {
        const long frameTicks = TimeSpan.TicksPerMillisecond * 120;

        return SpinnerFrame((int)(DateTime.UtcNow.Ticks / frameTicks));
    }

    public static string SpinnerFrame(int tick)
    {
        return SpinnerGlyphs[Math.Abs(tick) % SpinnerGlyphs.Length].ToString();
    }

    public static IRenderable Note(string text)
    {
        return new Markup($"[dim]{Markup.Escape(text)}[/]");
    }

    public static IRenderable InfoPanel(string title, string body)
    {
        return new Panel(new Markup(body))
        {
            Header = new PanelHeader($"[{Theme.Active.Ac}]{Markup.Escape(title)}[/]"),
            Border = BoxBorder.Rounded,
            BorderStyle = Theme.Active.BorderStyle,
            Padding = new Padding(2, 1),
            Expand = true,
        };
    }

    public static IRenderable WarningPanel(string body)
    {
        return new Panel(new Markup($"[{Theme.Active.Wa}]{Markup.Escape(body)}[/]"))
        {
            Header = new PanelHeader($"[{Theme.Active.Wa}]![/]"),
            Border = BoxBorder.Rounded,
            BorderStyle = Theme.Active.BorderStyle,
            Padding = new Padding(2, 1),
            Expand = true,
        };
    }

    public static IRenderable ErrorPanel(string title, string body)
    {
        return new Panel(new Markup(body))
        {
            Header = new PanelHeader($"[{Theme.Active.Er}]{Markup.Escape(title)}[/]"),
            Border = BoxBorder.Heavy,
            BorderStyle = Theme.Active.ErrorStyle,
            Padding = new Padding(2, 1),
            Expand = true,
        };
    }

    public static IRenderable CheckRow(string label, bool ok, string? detail = null)
    {
        var icon = ok ? $"[{Theme.Active.Su}]◆[/]" : $"[{Theme.Active.Er}]✗[/]";
        var extra = detail is null ? string.Empty : $" [dim]{Markup.Escape(detail)}[/]";

        return new Markup($"  {icon} {Markup.Escape(label)}{extra}");
    }

    private static readonly Dictionary<string, double> MemoryWeightsGb = new()
    {
        [FeatureCatalog.MediaProcessing.Id] = 1,
        [FeatureCatalog.DocumentPreviews.Id] = 1,
        [FeatureCatalog.AdaptiveStreaming.Id] = 1,
        [FeatureCatalog.AudioTaggingFast.Id] = 2,
        [FeatureCatalog.AudioTaggingDeep.Id] = 2,
        [FeatureCatalog.Lyrics.Id] = 0.5,
        [FeatureCatalog.Monitoring.Id] = 1.5,
    };

    public static IRenderable MemoryChart(FeatureSelection features)
    {
        var items = new List<BarChartItem>
        {
            new("Core system", 2.5, Theme.Active.ChartColors[^1]),
        };

        var colorIndex = 0;

        foreach (var (featureId, gb) in EnabledFeatureWeights(features))
        {
            var color = Theme.Active.ChartColors[colorIndex % Theme.Active.ChartColors.Count];

            colorIndex++;

            var feature = FeatureCatalog.All.First(f => f.Id == featureId);

            items.Add(new BarChartItem(feature.Label, gb, color));
        }

        return new BarChart()
            .Width(56)
            .Label("[dim]gigabytes of memory[/]")
            .CenterLabel()
            .WithMaxValue(6)
            .AddItems(items);
    }

    public static IRenderable MemoryBreakdown(FeatureSelection features)
    {
        var items = new List<IBreakdownChartItem>
        {
            new BreakdownChartItem("Core system", 2.5, Theme.Active.ChartColors[^1]),
        };

        var colorIndex = 0;

        foreach (var (featureId, gb) in EnabledFeatureWeights(features))
        {
            var color = Theme.Active.ChartColors[colorIndex % Theme.Active.ChartColors.Count];

            colorIndex++;

            var feature = FeatureCatalog.All.First(f => f.Id == featureId);

            items.Add(new BreakdownChartItem($"{feature.Label} ({gb:0.#} GB)", gb, color));
        }

        return new BreakdownChart()
            .Width(56)
            .AddItems(items);
    }

    private static IEnumerable<(string Id, double Gb)> EnabledFeatureWeights(FeatureSelection features)
    {
        foreach (var (id, gb) in MemoryWeightsGb)
        {
            if (features.IsEnabled(id))
            {
                yield return (id, gb);
            }
        }
    }

    public static IRenderable FileTree(FeatureSelection features)
    {
        var root = new Tree("[bold]./[/] [dim]- your Alexandria folder[/]")
            .Style(Theme.Active.BorderStyle);

        root.AddNode("[bold]docker-compose.yml[/] [dim]- how the services run[/]");
        root.AddNode("[bold].env[/] [dim]- all settings and passwords, locked to your user only[/]");
        root.AddNode("[bold]garage.toml[/] [dim]- storage configuration[/]");
        root.AddNode("[bold]alexandria-config.json[/] [dim]- record of your choices[/]");
        root.AddNode("[bold]init-output/[/] [dim]- temporary storage bootstrap files[/]");

        if (features.IsEnabled(FeatureCatalog.Monitoring.Id))
        {
            var prometheus = root.AddNode("[bold]prometheus/[/]");

            prometheus.AddNode("prometheus.yml [dim]- monitoring dashboard configuration[/]");
            prometheus.AddNode("secrets/ [dim]- its access token, kept out of the config[/]");
        }

        return root;
    }

    public static IRenderable SuccessGrid(InstallationContext context)
    {
        var httpPort = context.Config.Ports.GetValueOrDefault("HTTP_PORT", 80);
        var address = httpPort == 80 ? "http://localhost" : $"http://localhost:{httpPort}";

        var grid = new Grid().AddColumn(new GridColumn().PadRight(3)).AddColumn();

        grid.AddRow(
            new Markup($"[{Theme.Active.Ac}]Open Alexandria[/]"),
            new Markup($"[{Theme.Active.Ac}]{address}[/]"));

        if (context.Features.IsEnabled(FeatureCatalog.Monitoring.Id))
        {
            var grafanaPort = context.Config.Ports.GetValueOrDefault("GRAFANA_PORT", 3000);

            grid.AddRow(
                new Markup($"[{Theme.Active.Ac}]Monitoring[/]"),
                new Markup($"[{Theme.Active.Ac}]http://localhost:{grafanaPort}[/]"));
        }

        grid.AddRow(
            new Markup($"[{Theme.Active.Ac}]Your settings[/]"),
            new Markup($"{Markup.Escape(Path.Combine(context.InstallPath, ".env"))} [dim]- passwords live here[/]"));

        grid.AddRow(
            new Markup($"[{Theme.Active.Ac}]Stop later[/]"),
            new Markup($"[dim]cd[/] {Markup.Escape(context.InstallPath)} [dim]&&[/] docker compose down"));

        grid.AddRow(
            new Markup($"[{Theme.Active.Ac}]Start again[/]"),
            new Markup($"[dim]cd[/] {Markup.Escape(context.InstallPath)} [dim]&&[/] docker compose up -d"));

        grid.AddRow(
            new Markup($"[{Theme.Active.Ac}]See activity[/]"),
            new Markup($"[dim]cd[/] {Markup.Escape(context.InstallPath)} [dim]&&[/] docker compose logs -f"));

        return grid;
    }
}
