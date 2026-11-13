using Builder.UI.Shell.Screens;
using Spectre.Console;
using Spectre.Console.Rendering;

namespace Builder.UI.Shell.Splash;

// Paints a CityPlan at one moment of its reveal. The splash renders the whole
// map many times a second with a growing Elapsed; the success page renders a
// finished strip of the same city, so the install opens and closes on it.
internal sealed class CityScene : Renderable
{
    // Seconds until the last element (the prompt below the scene) is shown
    public const double Duration = 2.1;

    private const double ShadeStep = 0.06;
    private const double CartoucheStart = 0.8;
    private const double WordmarkStart = 0.95;
    private const double TaglineStart = 1.45;
    private const string Shades = " ░▒▓";
    private const string Park = "park";

    // Building shapes picked per block, mostly solid like the blocks of a
    // printed plan; a park, or a block still open while the city is built
    private static readonly string[] Growing = ["solid", "solid", "solid", "courtyard", "courtyard", "pair", Park, "open"];
    private static readonly string[] Finished = ["solid", "solid", "solid", "courtyard", "pair"];

    // Rebuilt only when the size changes, not on every animation frame
    private static readonly object PlanGate = new();
    private static CityPlan? _lastPlan;

    public required int Height { get; init; }

    // The full map with its cartouche and map key, rather than a strip
    public bool Splash { get; init; }

    // A finished city builds every block it has
    public bool Complete { get; init; }

    public double Elapsed { get; init; } = double.PositiveInfinity;

    protected override Measurement Measure(RenderOptions options, int maxWidth)
    {
        return new Measurement(maxWidth, maxWidth);
    }

    protected override IEnumerable<Segment> Render(RenderOptions options, int maxWidth)
    {
        var plan = PlanFor(maxWidth);
        var canvas = new SceneCanvas(maxWidth, Height);

        if (plan.Fits)
        {
            PaintWater(plan, canvas);
            PaintBlocks(plan, canvas);
            PaintStreets(plan, canvas);
            PaintLighthouse(plan, canvas);

            if (Splash)
            {
                PaintCartouche(plan, canvas);
                PaintLabels(plan, canvas);
                PaintCompass(plan, canvas);
                PaintScaleBar(plan, canvas);
            }
        }

        return canvas.ToSegments();
    }

    private CityPlan PlanFor(int width)
    {
        lock (PlanGate)
        {
            if (_lastPlan is not { } plan || plan.Width != width || plan.Height != Height || plan.Splash != Splash)
            {
                plan = new CityPlan { Width = width, Height = Height, Splash = Splash };
                _lastPlan = plan;
            }

            return plan;
        }
    }

    private bool Shown(double reveal) => Elapsed >= reveal;

    private void PaintWater(CityPlan plan, SceneCanvas canvas)
    {
        var waves = new Style(Theme.Active.WaterColor);

        for (var y = 0; y < Height; y++)
        {
            for (var x = 0; x < canvas.Width; x++)
            {
                if (!plan.IsWater(x, y) || !Shown(plan.WaterTime(x, y))) continue;

                canvas.Tint(x, y, Theme.Active.WaterFill);

                if (CityPlan.IsWave(x, y))
                {
                    canvas.Put(x, y, '~', waves);
                }
            }
        }
    }

    private void PaintBlocks(CityPlan plan, SceneCanvas canvas)
    {
        for (var y = 0; y < Height; y++)
        {
            for (var x = 0; x < canvas.Width; x++)
            {
                if (plan.BlockAt(x, y) is not { } block || !Shown(block.Start)) continue;

                var width = block.Right - block.Left - 1;
                var height = block.Bottom - block.Top - 1;
                var column = x - block.Left - 1;
                var row = y - block.Top - 1;
                var shape = Shape(block);

                if (shape == Park)
                {
                    if (width >= 3 && column % 2 == 1 && row == height / 2)
                    {
                        canvas.Put(x, y, '·', new Style(Theme.Active.SuccessColor));
                    }
                }
                else if (Built(shape, column, row, width, height))
                {
                    var level = Level(block);

                    canvas.Put(x, y, Shades[level], ShadeStyle(level, Landmark(block, level)));
                }
            }
        }
    }

    private string Shape(CityBlock block)
    {
        var shapes = Complete ? Finished : Growing;

        return shapes[(int)(CityPlan.Hash(block.Left, block.Top, 11) * shapes.Length)];
    }

    // Blocks fill to their streets; on small blocks every shape is solid
    private static bool Built(string shape, int column, int row, int width, int height)
    {
        return shape switch
        {
            "solid" => true,
            "courtyard" => !(width >= 5 && height >= 3 && column >= 1 && column < width - 1 && row >= 1 && row < height - 1),
            "pair" => !(width >= 5 && column == width / 2),
            _ => false,
        };
    }

    // Blocks rise one shade at a time to their final density
    private int Level(CityBlock block)
    {
        var roll = CityPlan.Hash(block.Left, block.Top, 5);
        int final;

        if (roll < 0.55 || Complete)
        {
            final = roll < 0.75 ? 3 : 2;
        }
        else
        {
            final = roll < 0.85 ? 2 : 1;
        }

        if (double.IsPositiveInfinity(Elapsed)) return final;

        return Math.Min(final, (int)((Elapsed - block.Start) / ShadeStep) + 1);
    }

    private static bool Landmark(CityBlock block, int level)
    {
        return level == 3 && CityPlan.Hash(block.Left, block.Top, 17) < 0.2;
    }

    private static Style ShadeStyle(int level, bool landmark)
    {
        if (landmark) return new Style(Theme.Active.AccentSecondary);

        return level == 1 ? new Style(Theme.Active.BorderColor) : new Style(Theme.Active.MutedColor);
    }

    private void PaintStreets(CityPlan plan, SceneCanvas canvas)
    {
        var minor = new Style(Theme.Active.BorderColor);
        var major = new Style(Theme.Active.Accent);

        for (var y = 0; y < Height; y++)
        {
            for (var x = 0; x < canvas.Width; x++)
            {
                var up = Visible(plan.SouthEdge(x, y - 1));
                var down = Visible(plan.SouthEdge(x, y));
                var left = Visible(plan.EastEdge(x - 1, y));
                var right = Visible(plan.EastEdge(x, y));

                if (up.Weight + down.Weight + left.Weight + right.Weight == 0) continue;

                var isMajor = up.Major || down.Major || left.Major || right.Major;
                var glyph = BoxGlyphs.For(up.Weight, down.Weight, left.Weight, right.Weight);

                // Conjectured streets stay dashed along their length, as on the plan
                if (glyph == '─' && left.Dashed && right.Dashed)
                {
                    glyph = '┄';
                }
                else if (glyph == '│' && up.Dashed && down.Dashed)
                {
                    glyph = '┆';
                }

                canvas.Put(x, y, glyph, isMajor ? major : minor);
            }
        }
    }

    private (int Weight, bool Major, bool Dashed) Visible(CityEdge? edge)
    {
        if (edge is not { } shown || !Shown(shown.Reveal)) return (BoxGlyphs.None, false, false);

        return (shown.Weight, shown.Major, shown.Dashed);
    }

    // The Pharos stood on an islet off the island's eastern tip; it is lit last
    private void PaintLighthouse(CityPlan plan, SceneCanvas canvas)
    {
        if (plan.Lighthouse is not (var x, var y)) return;

        // Its islet, too small to survive the coastline's tracing
        for (var column = x - 1; column <= x + 1; column++)
        {
            canvas.Clear(column, y);
        }

        if (Shown(CityPlan.LighthouseTime))
        {
            canvas.Put(x, y, '▲', new Style(Theme.Active.WarningColor, decoration: Decoration.Bold));
        }
    }

    // The title box of the plan: drawn round from its top-left corner, then
    // the wordmark raised inside it like the blocks around it, and the
    // tagline and version lettered into its frame
    private void PaintCartouche(CityPlan plan, SceneCanvas canvas)
    {
        if (plan.Cartouche is not (var left, var top) || !Shown(CartoucheStart)) return;

        const int width = CityPlan.CartoucheWidth;
        const int height = CityPlan.CartoucheHeight;

        var frame = new Style(Theme.Active.AccentSecondary);
        var perimeter = 2 * (width + height);

        for (var row = 0; row < height; row++)
        {
            for (var column = 0; column < width; column++)
            {
                canvas.Clear(left + column, top + row);

                var border = FrameGlyph(column, row, width, height);

                if (border != ' ' && Shown(CartoucheStart + 0.25 * PerimeterIndex(column, row, width, height) / perimeter))
                {
                    canvas.Put(left + column, top + row, border, frame);
                }
            }
        }

        PaintWordmark(canvas, left + (width - Views.Wordmark[0].Length) / 2, top + 1);
        Type(canvas, $" {Views.Tagline} ", left + (width - Views.Tagline.Length - 2) / 2, top + height - 1, TaglineStart, frame);
        Type(canvas, $" {Views.InstallerVersion} ", left + width - Views.InstallerVersion.Length - 5, top, TaglineStart + 0.3, new Style(Theme.Active.MutedColor));
    }

    private static char FrameGlyph(int column, int row, int width, int height)
    {
        var edgeRow = row == 0 || row == height - 1;
        var edgeColumn = column == 0 || column == width - 1;

        if (edgeRow && edgeColumn)
        {
            if (row == 0) return column == 0 ? '╔' : '╗';

            return column == 0 ? '╚' : '╝';
        }

        if (edgeRow) return '═';

        return edgeColumn ? '║' : ' ';
    }

    // Distance clockwise round the frame from its top-left corner
    private static int PerimeterIndex(int column, int row, int width, int height)
    {
        if (row == 0) return column;
        if (column == width - 1) return width + row;
        if (row == height - 1) return width + height + (width - 1 - column);

        return 2 * width + height + (height - 1 - row);
    }

    private void PaintWordmark(SceneCanvas canvas, int left, int top)
    {
        var shade = new Style(Theme.Active.AccentSecondary);
        var final = new Style(Theme.Active.Accent, decoration: Decoration.Bold);
        var wordmark = Views.Wordmark;

        for (var row = 0; row < wordmark.Length; row++)
        {
            var line = wordmark[row];

            for (var i = 0; i < line.Length; i++)
            {
                var start = WordmarkStart + 0.45 * i / line.Length + 0.1 * CityPlan.Hash(i, row, 23);

                if (line[i] == ' ' || Elapsed < start) continue;

                var step = (int)((Elapsed - start) / ShadeStep);

                if (step < 3)
                {
                    canvas.Put(left + i, top + row, Shades[step + 1], shade);
                }
                else
                {
                    canvas.Put(left + i, top + row, line[i], final);
                }
            }
        }
    }

    private void PaintLabels(CityPlan plan, SceneCanvas canvas)
    {
        var muted = new Style(Theme.Active.MutedColor);
        var street = new Style(Theme.Active.AccentSecondary, decoration: Decoration.Bold);

        foreach (var label in plan.Labels)
        {
            // Water names keep a little open water around them
            if (!label.OnStreet && Shown(TaglineStart))
            {
                for (var x = label.X - 1; x <= label.X + label.Text.Length; x++)
                {
                    canvas.Put(x, label.Y, ' ', Style.Plain);
                }
            }

            Type(canvas, label.Text, label.X, label.Y, TaglineStart, label.OnStreet ? street : muted);
        }
    }

    // North sits 22.75° east of straight up because the map is turned to
    // its street grid; the rose is drawn turned the same way
    private void PaintCompass(CityPlan plan, SceneCanvas canvas)
    {
        if (plan.Compass is not (var left, var top) || !Shown(TaglineStart)) return;

        var muted = new Style(Theme.Active.MutedColor);

        for (var row = 0; row < CityPlan.CompassHeight; row++)
        {
            for (var column = 0; column < CityPlan.CompassWidth; column++)
            {
                canvas.Put(left + column, top + row, ' ', Style.Plain);
            }
        }

        canvas.Put(left + 6, top, 'N', new Style(Theme.Active.Accent, decoration: Decoration.Bold));
        canvas.Put(left + 5, top + 1, '·', muted);
        canvas.Put(left, top + 1, 'W', muted);
        canvas.Put(left + 2, top + 2, '·', muted);
        canvas.Put(left + 4, top + 2, '+', muted);
        canvas.Put(left + 6, top + 2, '·', muted);
        canvas.Put(left + 3, top + 3, '·', muted);
        canvas.Put(left + 8, top + 3, 'E', muted);
        canvas.Put(left + 2, top + 4, 'S', muted);
    }

    // Drawn to the true scale of this projection
    private void PaintScaleBar(CityPlan plan, SceneCanvas canvas)
    {
        if (plan.ScaleBar is not (var left, var y, var length)) return;

        var bar = "├" + new string('─', length - 2) + "┤ 1 km";

        Type(canvas, bar, left, y, TaglineStart, new Style(Theme.Active.MutedColor));
    }

    private void Type(SceneCanvas canvas, string text, int left, int y, double start, Style style)
    {
        for (var i = 0; i < text.Length; i++)
        {
            if (Shown(start + 0.012 * i))
            {
                canvas.Put(left + i, y, text[i], style);
            }
        }
    }
}
