using Spectre.Console;
using Spectre.Console.Rendering;

namespace Builder.UI.Shell.Splash;

// A fixed grid of single-width cells, each with its own style, turned into
// segments with runs of equal style merged so a full-screen frame stays a
// few hundred segments rather than thousands. A cell can be tinted; whatever
// is drawn there later keeps the tint as its background.
internal sealed class SceneCanvas
{
    private readonly char[] _glyphs;
    private readonly Style[] _styles;
    private readonly Color?[] _tints;

    public SceneCanvas(int width, int height)
    {
        Width = Math.Max(0, width);
        Height = Math.Max(0, height);
        _glyphs = new char[Width * Height];
        _styles = new Style[Width * Height];
        _tints = new Color?[Width * Height];
        Array.Fill(_glyphs, ' ');
        Array.Fill(_styles, Style.Plain);
    }

    public int Width { get; }

    public int Height { get; }

    public void Tint(int x, int y, Color background)
    {
        if (x < 0 || y < 0 || x >= Width || y >= Height) return;

        _tints[y * Width + x] = background;
        _styles[y * Width + x] = _styles[y * Width + x].Combine(new Style(background: background));
    }

    // Back to plain, untinted blank, for boxes drawn over water
    public void Clear(int x, int y)
    {
        if (x < 0 || y < 0 || x >= Width || y >= Height) return;

        _tints[y * Width + x] = null;
        _glyphs[y * Width + x] = ' ';
        _styles[y * Width + x] = Style.Plain;
    }

    public void Put(int x, int y, char glyph, Style style)
    {
        if (x < 0 || y < 0 || x >= Width || y >= Height) return;

        var index = y * Width + x;

        _glyphs[index] = glyph;
        _styles[index] = _tints[index] is { } tint ? style.Combine(new Style(background: tint)) : style;
    }

    public IEnumerable<Segment> ToSegments()
    {
        for (var y = 0; y < Height; y++)
        {
            var start = y * Width;
            var runStart = start;

            for (var i = start + 1; i <= start + Width; i++)
            {
                if (i < start + Width && _styles[i].Equals(_styles[runStart])) continue;

                yield return new Segment(new string(_glyphs, runStart, i - runStart), _styles[runStart]);
                runStart = i;
            }

            yield return Segment.LineBreak;
        }
    }
}
