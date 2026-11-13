using Spectre.Console;
using Spectre.Console.Rendering;

namespace Builder.UI.Shell.Screens;

// Content behind a colored left bar. Lighter than a boxed panel, so notices
// read as part of the page instead of a dialog stacked on top of it.
internal sealed class Callout(IRenderable content, Style barStyle) : Renderable
{
    private const string Bar = "▌ ";

    protected override Measurement Measure(RenderOptions options, int maxWidth)
    {
        var inner = content.Measure(options, Math.Max(1, maxWidth - Bar.Length));

        return new Measurement(inner.Min + Bar.Length, Math.Min(maxWidth, inner.Max + Bar.Length));
    }

    protected override IEnumerable<Segment> Render(RenderOptions options, int maxWidth)
    {
        var innerWidth = Math.Max(1, maxWidth - Bar.Length);
        var lines = Segment.SplitLines(content.Render(options, innerWidth), innerWidth);

        foreach (var line in lines)
        {
            yield return new Segment(Bar, barStyle);

            foreach (var segment in line)
            {
                yield return segment;
            }

            yield return Segment.LineBreak;
        }
    }
}
