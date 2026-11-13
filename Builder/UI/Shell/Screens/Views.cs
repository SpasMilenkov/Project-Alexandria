using Builder.Models;
using Builder.Services;
using Spectre.Console;
using Spectre.Console.Rendering;

namespace Builder.UI.Shell.Screens;

// Renderable building blocks shared by every screen, so headings, notices,
// lists and facts look the same wherever they appear.
public static class Views
{
    // Two rows of block glyphs: narrow enough for an 80-column terminal,
    // unlike a figlet banner that wraps under the content cap, and drawn
    // from block elements every terminal font ships
    internal static readonly string[] Wordmark =
    [
        "▄▀█ █   █▀▀ ▀▄▀ ▄▀█ █▄ █ █▀▄ █▀█ █ ▄▀█",
        "█▀█ █▄▄ ██▄ █ █ █▀█ █ ▀█ █▄▀ █▀▄ █ █▀█",
    ];

    internal const string Tagline = "self-hosted files, media and music";
    internal const string InstallerVersion = "installer v0.4";

    private const string SpinnerGlyphs = "⠋⠙⠹⠸⠼⠴⠦⠧⠇⠏";

    public static IRenderable Banner()
    {
        var wordmark = string.Join("\n", Wordmark.Select(line => $"[bold {Theme.Active.Ac}]{line}[/]"));

        return new Rows(
            new Markup(wordmark),
            new Markup($"[{Theme.Active.As}]{Tagline}[/]  {Ui.Muted(InstallerVersion)}"),
            Blank());
    }

    public static IRenderable Title(string text)
    {
        return new Markup($"[bold]{Markup.Escape(text)}[/]");
    }

    // A page heading with its lead paragraph and breathing room below
    public static IRenderable Heading(string title, params string[] notes)
    {
        var rows = new List<IRenderable> { Title(title) };

        rows.AddRange(notes.Select(Note));
        rows.Add(Blank());

        return new Rows(rows);
    }

    public static IRenderable Section(string title)
    {
        return new Rule($"[{Theme.Active.Ac}]{Markup.Escape(title)}[/]")
            .LeftJustified()
            .RuleStyle(Theme.Active.BorderStyle);
    }

    public static IRenderable Note(string text)
    {
        return new Markup(Ui.Muted(text));
    }

    public static IRenderable Highlight(string text)
    {
        return new Markup($"[bold {Theme.Active.Ac}]{Markup.Escape(text)}[/]");
    }

    public static IRenderable Blank()
    {
        return new Text(string.Empty);
    }

    public static string SpinnerFrame()
    {
        const long frameTicks = TimeSpan.TicksPerMillisecond * 120;

        // Wall-clock derived so the spinner moves on every repaint without
        // anything counting ticks
        return SpinnerFrame((int)(DateTime.UtcNow.Ticks / frameTicks));
    }

    public static string SpinnerFrame(int tick)
    {
        return SpinnerGlyphs[Math.Abs(tick) % SpinnerGlyphs.Length].ToString();
    }

    // Body arguments are markup; callers escape any value they did not write
    public static IRenderable InfoPanel(string title, string body)
    {
        return Notice("●", Theme.Active.As, title, body);
    }

    public static IRenderable WarningPanel(string body)
    {
        return Notice("!", Theme.Active.Wa, null, Markup.Escape(body));
    }

    public static IRenderable ErrorPanel(string title, string body)
    {
        return Notice("✗", Theme.Active.Er, title, body);
    }

    public static IRenderable SuccessPanel(string title, string body)
    {
        return Notice("✓", Theme.Active.Su, title, body);
    }

    private static IRenderable Notice(string icon, string color, string? title, string body)
    {
        var rows = new List<IRenderable>();

        if (title is not null)
        {
            rows.Add(new Markup($"[bold {color}]{icon} {Markup.Escape(title)}[/]"));
            rows.Add(new Markup(body));
        }
        else
        {
            rows.Add(new Markup($"[bold {color}]{icon}[/] {body}"));
        }

        return new Rows(new Callout(new Rows(rows), Style.Parse(color)), Blank());
    }

    public static IRenderable CheckList(IEnumerable<(string Label, bool Ok, string? Detail)> checks)
    {
        var grid = new Grid()
            .AddColumn(new GridColumn().NoWrap().PadRight(1))
            .AddColumn(new GridColumn().NoWrap().PadRight(2))
            .AddColumn();

        foreach (var (label, ok, detail) in checks)
        {
            var icon = ok ? Ui.Success("✓") : Ui.Error("✗");

            grid.AddRow(new Markup(icon), new Markup(Markup.Escape(label)), new Markup(Ui.Muted(detail ?? string.Empty)));
        }

        return grid;
    }

    // Label/value pairs with the labels in one aligned, muted column
    public static IRenderable Facts(IEnumerable<(string Label, string Value)> facts)
    {
        var grid = new Grid()
            .AddColumn(new GridColumn().NoWrap().PadRight(3))
            .AddColumn();

        foreach (var (label, value) in facts)
        {
            grid.AddRow(new Markup(Ui.Muted(label)), new Markup(Markup.Escape(value)));
        }

        return grid;
    }

    // A small sample of a palette, drawn in the candidate's own colors rather
    // than the active theme's, so looks can be compared before choosing
    public static IRenderable ThemeSample(ThemeDefinition definition)
    {
        var swatches = string.Join(" ", new[]
        {
            definition.Ac, definition.As, definition.Su, definition.Wa, definition.Er,
        }.Select(color => $"[{color}]██[/]"));

        var content = new Rows(
            new Markup($"[bold {definition.Ac}]{Markup.Escape(definition.DisplayName)}[/]  [{definition.Mu}]{Markup.Escape(definition.Tagline)}[/]"),
            new Markup(swatches),
            new Markup($"[{definition.Ac}]❯ Selected item[/]  [{definition.Su}]✓ Ready[/]  [{definition.Wa}]! Close to the limit[/]"));

        return new Callout(content, new Style(definition.Accent));
    }

    public static IRenderable MemoryChart(FeatureSelection features)
    {
        var estimates = ServiceCatalog.Resolve(features).MemoryEstimates;
        var items = estimates.Select(estimate => new BarChartItem(
            estimate.Label, estimate.MemoryMb / 1024.0,
            MemoryBudget.GroupColor(estimate.Label))).ToArray();

        return new BarChart()
            .Width(60)
            .Label(Ui.Muted("gigabytes of memory, including headroom"))
            .WithMaxValue(Math.Max(6, estimates.Max(estimate => estimate.MemoryMb / 1024.0)))
            .AddItems(items);
    }

    public static IRenderable MemoryBreakdown(FeatureSelection features)
    {
        var items = ServiceCatalog.Resolve(features).MemoryEstimates.Select(estimate =>
            new BreakdownChartItem($"{estimate.Label} ({estimate.MemoryMb / 1024.0:0.#} GB)",
                estimate.MemoryMb / 1024.0,
                MemoryBudget.GroupColor(estimate.Label))).ToArray();

        return new BreakdownChart()
            .Width(60)
            .HideTagValues()
            .AddItems(items);
    }

    public static IRenderable FileTree(FeatureSelection features)
    {
        string Entry(string name, string description) => $"[bold]{Markup.Escape(name)}[/]  {Ui.Muted(description)}";

        var root = new Tree(Entry("./", "your Alexandria settings folder"))
            .Style(Theme.Active.BorderStyle);

        root.AddNode(Entry("docker-compose.yml", "how the services run"));
        root.AddNode(Entry(".env", "all settings and passwords, locked to your user only"));
        root.AddNode(Entry("garage.toml", "storage configuration"));
        root.AddNode(Entry("alexandria-config.json", "record of your choices"));
        root.AddNode(Entry("init-output/", "temporary storage bootstrap files"));

        if (features.IsEnabled(FeatureCatalog.Monitoring.Id))
        {
            var prometheus = root.AddNode(Entry("prometheus/", "monitoring"));

            prometheus.AddNode(Entry("prometheus.yml", "monitoring dashboard configuration"));
            prometheus.AddNode(Entry("secrets/", "its access token, kept out of the config"));
        }

        return root;
    }
}
