using System.Diagnostics;
using Spectre.Console;
using Spectre.Console.Rendering;

namespace Builder.UI.Shell.Screens;

// The services a feature runs, one chip each. Right after the feature is
// toggled they light up once, green when added and red when removed, then
// settle; the shell's repaint heartbeat carries the change, so nothing
// keeps moving once it has played.
internal sealed class ServiceChips : Renderable
{
    private static readonly TimeSpan FlashLength = TimeSpan.FromMilliseconds(600);

    public required IReadOnlyList<string> Names { get; init; }

    public required bool Running { get; init; }

    // Which way the feature was just toggled and when, if it was
    public (bool TurnedOn, long At)? Toggled { get; init; }

    protected override Measurement Measure(RenderOptions options, int maxWidth)
    {
        var widest = Names.Count == 0 ? 0 : Names.Max(name => name.Length + 2);

        return new Measurement(Math.Min(widest, maxWidth), maxWidth);
    }

    protected override IEnumerable<Segment> Render(RenderOptions options, int maxWidth)
    {
        return ((IRenderable)new Markup(Build(maxWidth))).Render(options, maxWidth);
    }

    private string Build(int width)
    {
        var style = ChipStyle();
        var lines = new List<string> { string.Empty };
        var lineWidth = 0;

        // Wrapped by hand so a chip is never split across lines
        foreach (var name in Names)
        {
            var cells = name.Length + 2;

            if (lineWidth > 0 && lineWidth + 1 + cells > width)
            {
                lines.Add(string.Empty);
                lineWidth = 0;
            }

            if (lineWidth > 0)
            {
                lines[^1] += " ";
                lineWidth += 1;
            }

            lines[^1] += $"[{style}] {Markup.Escape(name)} [/]";
            lineWidth += cells;
        }

        return string.Join("\n", lines);
    }

    private string ChipStyle()
    {
        if (Toggled is { } toggled && Stopwatch.GetElapsedTime(toggled.At) < FlashLength)
        {
            return $"black on {(toggled.TurnedOn ? Theme.Active.Su : Theme.Active.Er)}";
        }

        return Running ? $"{Theme.Active.As} on {Theme.Active.Bo}" : Theme.Active.Mu;
    }
}
