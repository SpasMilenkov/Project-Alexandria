using Spectre.Console;
using Spectre.Console.Rendering;

namespace Builder.UI.Shell;

// Already rendered lines replayed as a renderable, so the shell can render
// main-pane content once, measure it and show only the slice that fits.
internal sealed class SegmentLines(IReadOnlyList<SegmentLine> lines) : Renderable
{
    public static List<SegmentLine> Render(IRenderable renderable, int width)
    {
        var options = new RenderOptions(AnsiConsole.Profile.Capabilities, new Size(width, AnsiConsole.Profile.Height));

        return Segment.SplitLines(renderable.Render(options, width), width);
    }

    protected override Measurement Measure(RenderOptions options, int maxWidth)
    {
        var widest = lines.Count == 0 ? 0 : lines.Max(line => line.CellCount());

        return new Measurement(Math.Min(widest, maxWidth), maxWidth);
    }

    protected override IEnumerable<Segment> Render(RenderOptions options, int maxWidth)
    {
        foreach (var line in lines)
        {
            foreach (var segment in line)
            {
                yield return segment;
            }

            yield return Segment.LineBreak;
        }
    }
}
