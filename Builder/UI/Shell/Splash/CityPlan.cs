namespace Builder.UI.Shell.Splash;

// AlexandriaMap projected onto a grid of terminal cells, with everything the
// scene needs worked out once per size: which cells are water, which streets
// survive at this scale, the blocks between them and where the title
// cartouche, place names, compass and scale bar fit without covering the city.
//
// Reveal times are seconds from the start of the splash. The Canopic Way and
// the cross street through the palace quarter grow out of their crossing,
// every other street grows out of one of them, and the causeway reaches out
// to Pharos after them, so the city is laid out in front of the viewer.
internal sealed class CityPlan
{
    public const int MinSplashWidth = 70;
    public const int MinSplashHeight = 22;
    public const int CartoucheWidth = 44;
    public const int CartoucheHeight = 4;
    public const int CompassWidth = 9;
    public const int CompassHeight = 5;
    public const double LighthouseTime = 1.8;

    // Streets closer than this merge into one line at small sizes, so every
    // block keeps at least one row and two columns inside it
    private const int MinRowGap = 2;
    private const int MinColumnGap = 3;

    private const double CrossingTime = 0.15;
    private const double AcrossTime = 0.6;
    private const double AvenueRowTime = 0.045;
    private const double StreetRowTime = 0.035;
    private const double CausewayStart = 0.55;
    private const double CausewayStepTime = 0.035;

    private readonly Lazy<bool> _built;
    private readonly Dictionary<int, StreetCells> _rows = new();
    private readonly Dictionary<int, StreetCells> _columns = new();
    private readonly Dictionary<(int X, int Y, bool South), double> _causeway = new();
    private readonly List<PlacedLabel> _labels = [];

    private double _unitsPerColumn;
    private double _left;
    private double _top;
    private bool[] _land = [];
    private bool[] _occupied = [];
    private CityBlock?[] _blocks = [];
    private int _crossX;
    private int _crossY;
    private double _canopicReach = 1;
    private (int X, int Y)? _lighthouse;
    private (int X, int Y)? _cartouche;
    private (int X, int Y)? _compass;
    private (int X, int Y, int Length)? _scaleBar;

    public CityPlan()
    {
        _built = new Lazy<bool>(Build);
    }

    public required int Width { get; init; }
    public required int Height { get; init; }

    // The opening map with its cartouche and map key; otherwise a finished
    // strip of the city centred on the Canopic Way
    public bool Splash { get; init; }

    public bool Fits => Splash
        ? Width >= MinSplashWidth && Height >= MinSplashHeight
        : Width >= 24 && Height >= 3;

    public (int X, int Y)? Lighthouse => Ready(_lighthouse);

    public (int X, int Y)? Cartouche => Ready(_cartouche);

    public (int X, int Y)? Compass => Ready(_compass);

    public (int X, int Y, int Length)? ScaleBar => Ready(_scaleBar);

    public IReadOnlyList<PlacedLabel> Labels => Ready(_labels);

    public bool IsWater(int x, int y)
    {
        Ready(true);

        if (!InView(x, y))
        {
            var (u, v) = CellCentre(x, y);

            return !IsLandAt(u, v);
        }

        return !_land[y * Width + x];
    }

    // Sparse wave marks in a brick pattern
    public static bool IsWave(int x, int y) => Mod(x + 6 * (y % 2), 12) == 0;

    // The water floods in from the west before anything is built
    public double WaterTime(int x, int y) => 0.25 * x / Math.Max(1, Width) + 0.08 * Hash(x, y, 7);

    // The street between (x, y) and (x + 1, y)
    public CityEdge? EastEdge(int x, int y)
    {
        Ready(true);

        if (_causeway.TryGetValue((x, y, false), out var causeway)) return Major(causeway);
        if (!_rows.TryGetValue(y, out var row) || x < row.From || x + 1 > row.To) return null;
        if (IsWater(x, y) || IsWater(x + 1, y)) return null;

        return row.Major ? Major(CanopicTime(x + 0.5)) : Minor(RowTime(y, row, x + 0.5), row.Conjectured);
    }

    // The street between (x, y) and (x, y + 1)
    public CityEdge? SouthEdge(int x, int y)
    {
        Ready(true);

        if (_causeway.TryGetValue((x, y, true), out var causeway)) return Major(causeway);
        if (!_columns.TryGetValue(x, out var column) || y < column.From || y + 1 > column.To) return null;
        if (IsWater(x, y) || IsWater(x, y + 1)) return null;

        return column.Major ? Major(SomaTime(y + 0.5)) : Minor(ColumnTime(x, column, y + 0.5), column.Conjectured);
    }

    public CityBlock? BlockAt(int x, int y)
    {
        Ready(true);

        return InView(x, y) ? _blocks[y * Width + x] : null;
    }

    // Stable pseudo-random value in [0, 1), so the same city is built every time
    public static double Hash(int a, int b, int salt)
    {
        unchecked
        {
            var h = (uint)(a * 73856093) ^ (uint)(b * 19349663) ^ (uint)(salt * 83492791);

            h ^= h >> 13;
            h *= 0x5bd1e995;
            h ^= h >> 15;

            return (h & 0xFFFFFF) / (double)0x1000000;
        }
    }

    private T Ready<T>(T value)
    {
        _ = _built.Value;

        return value;
    }

    private bool Build()
    {
        if (!Fits) return true;

        // The cartouche goes over open water beside the city when there is
        // room; otherwise a band of sea is added above the map to hold it
        Layout(reserve: 0);

        if (!Splash) return true;

        if (!PlaceCartouche())
        {
            Layout(reserve: CartoucheHeight + 1);
            _cartouche = ((Width - CartoucheWidth) / 2, 0);
            Occupy(_cartouche.Value.X - 1, 0, CartoucheWidth + 2, CartoucheHeight + 1);
        }

        PlaceMapKey();

        return true;
    }

    private void Layout(int reserve)
    {
        Project(reserve);

        _land = new bool[Width * Height];
        _occupied = new bool[Width * Height];
        _blocks = new CityBlock?[Width * Height];
        _rows.Clear();
        _columns.Clear();
        _causeway.Clear();
        _labels.Clear();

        for (var y = 0; y < Height; y++)
        {
            for (var x = 0; x < Width; x++)
            {
                var (u, v) = CellCentre(x, y);

                _land[y * Width + x] = IsLandAt(u, v);
            }
        }

        KeepStreets(AlexandriaMap.LongStreets, _rows, ToRow, ToColumn, MinRowGap, Height);
        KeepStreets(AlexandriaMap.CrossStreets, _columns, ToColumn, ToRow, MinColumnGap, Width);
        Trim(_columns, _rows);

        var canopic = _rows.FirstOrDefault(row => row.Value.Major);
        var soma = _columns.FirstOrDefault(column => column.Value.Major);

        _crossY = canopic.Key;
        _crossX = soma.Key;
        _canopicReach = Math.Max(1, Math.Max(_crossX - canopic.Value.From, canopic.Value.To - _crossX));

        LayCauseway();
        FindBlocks();

        var (lightX, lightY) = ToCell(AlexandriaMap.Lighthouse.U, AlexandriaMap.Lighthouse.V);

        _lighthouse = InView(lightX, lightY) ? (lightX, lightY) : null;
        Occupy(lightX - 1, lightY, 3, 1);
    }

    // Fit the whole map in the window, or for the strip fill its width
    private void Project(int reserve)
    {
        var spanU = AlexandriaMap.Right - AlexandriaMap.Left;
        var spanV = AlexandriaMap.Bottom - AlexandriaMap.Top;
        var centreU = (AlexandriaMap.Left + AlexandriaMap.Right) / 2;

        if (!Splash)
        {
            _unitsPerColumn = spanU / Width;
            _left = centreU - Width * _unitsPerColumn / 2;
            _top = AlexandriaMap.CityMiddle - Height * _unitsPerColumn;

            return;
        }

        var rows = Height - reserve;

        // A cell is about twice as tall as it is wide, so a row spans twice
        // the map distance of a column
        _unitsPerColumn = Math.Max(spanU / Width, spanV / (2.0 * rows));
        _left = centreU - Width * _unitsPerColumn / 2;
        _top = (AlexandriaMap.Top + AlexandriaMap.Bottom) / 2 - (rows + 2 * reserve) * _unitsPerColumn;
    }

    private (double U, double V) CellCentre(int x, int y) =>
        (_left + (x + 0.5) * _unitsPerColumn, _top + (y + 0.5) * 2 * _unitsPerColumn);

    private int ToColumn(double u) => (int)Math.Floor((u - _left) / _unitsPerColumn);

    private int ToRow(double v) => (int)Math.Floor((v - _top) / (2 * _unitsPerColumn));

    private (int X, int Y) ToCell(double u, double v) => (ToColumn(u), ToRow(v));

    private bool InView(int x, int y) => x >= 0 && y >= 0 && x < Width && y < Height;

    private static bool IsLandAt(double u, double v)
    {
        // Past the traced area the outline's edge simply continues
        u = Math.Clamp(u, -415, 1315);
        v = Math.Clamp(v, 65, 1415);

        var inside = false;

        foreach (var outline in AlexandriaMap.Land)
        {
            for (int i = 0, j = outline.Length - 2; i < outline.Length; j = i, i += 2)
            {
                double u1 = outline[i], v1 = outline[i + 1], u2 = outline[j], v2 = outline[j + 1];

                if (v1 > v != v2 > v && u < u1 + (v - v1) * (u2 - u1) / (v2 - v1))
                {
                    inside = !inside;
                }
            }
        }

        return inside;
    }

    // The major street always stays; the others are kept in plan order while
    // they leave room for a block on either side
    private static void KeepStreets(
        IEnumerable<MapStreet> streets,
        Dictionary<int, StreetCells> kept,
        Func<double, int> across,
        Func<double, int> along,
        int minGap,
        int limit)
    {
        // Conjectured streets give way first when the scale is too small for all
        foreach (var street in streets.OrderByDescending(street => street.Major).ThenBy(street => street.Conjectured))
        {
            var at = across(street.Position);

            if (at < 0 || at >= limit) continue;
            if (!street.Major && kept.Keys.Any(other => Math.Abs(other - at) < minGap)) continue;

            kept[at] = new StreetCells
            {
                From = along(street.From),
                To = along(street.To),
                Major = street.Major,
                Conjectured = street.Conjectured,
            };
        }
    }

    // Cross streets end at the last long street they meet, so none trails
    // off into open ground once others are dropped at small sizes; the long
    // streets keep their length, which is where the city wall stood
    private static void Trim(Dictionary<int, StreetCells> streets, Dictionary<int, StreetCells> crossing)
    {
        foreach (var (at, street) in streets.ToArray())
        {
            if (street.Major) continue;

            var crossings = crossing
                .Where(other => other.Value.Covers(at) && street.Covers(other.Key))
                .Select(other => other.Key)
                .ToArray();

            if (crossings.Length < 2)
            {
                streets.Remove(at);

                continue;
            }

            streets[at] = street with { From = crossings.Min(), To = crossings.Max() };
        }
    }

    // The Heptastadion as a chain of cells, each step one cell across or
    // down, whichever keeps the chain nearest the true line, drawn from the
    // mainland outwards
    private void LayCauseway()
    {
        var points = AlexandriaMap.Causeway;
        var step = 0;

        for (var i = 0; i + 1 < points.Length; i++)
        {
            var (startX, startY) = ToCell(points[i].U, points[i].V);
            var (endX, endY) = ToCell(points[i + 1].U, points[i + 1].V);
            var (x, y) = (startX, startY);

            while ((x, y) != (endX, endY))
            {
                var acrossDone = Progress(x, startX, endX);
                var downDone = Progress(y, startY, endY);
                var reveal = CausewayStart + CausewayStepTime * step++;

                if (x != endX && (y == endY || acrossDone <= downDone))
                {
                    var next = x + Math.Sign(endX - x);

                    _causeway[(Math.Min(x, next), y, false)] = reveal;
                    x = next;
                }
                else
                {
                    var next = y + Math.Sign(endY - y);

                    _causeway[(x, Math.Min(y, next), true)] = reveal;
                    y = next;
                }

                Occupy(x, y, 1, 1);
            }
        }
    }

    private static double Progress(int at, int start, int end) =>
        start == end ? 1 : Math.Abs(at - start) / (double)Math.Abs(end - start);

    private void FindBlocks()
    {
        var rows = _rows.OrderBy(row => row.Key).ToArray();
        var columns = _columns.OrderBy(column => column.Key).ToArray();

        for (var y = 0; y < Height; y++)
        {
            for (var x = 0; x < Width; x++)
            {
                if (!_land[y * Width + x]) continue;

                if (OnStreet(x, y))
                {
                    Occupy(x, y, 1, 1);

                    continue;
                }

                var above = Nearest(rows.Where(row => row.Key < y && row.Value.Covers(x)).Reverse());
                var below = Nearest(rows.Where(row => row.Key > y && row.Value.Covers(x)));
                if (above is not { } top || below is not { } bottom) continue;

                // Without a cross street on one side, the block runs to where
                // its long streets end
                var west = Nearest(columns.Where(column => column.Key < x && column.Value.Covers(y)).Reverse())
                           ?? Math.Max(_rows[top].From, _rows[bottom].From) - 1;
                var east = Nearest(columns.Where(column => column.Key > x && column.Value.Covers(y)))
                           ?? Math.Min(_rows[top].To, _rows[bottom].To) + 1;

                if (x <= west || x >= east) continue;

                _blocks[y * Width + x] = new CityBlock
                {
                    Left = west,
                    Top = top,
                    Right = east,
                    Bottom = bottom,
                    Start = RowTime(top, _rows[top], x + 0.5) + 0.1 + 0.3 * Hash(west, top, 3),
                };
                Occupy(x, y, 1, 1);
            }
        }
    }

    private static int? Nearest(IEnumerable<KeyValuePair<int, StreetCells>> streets)
    {
        foreach (var street in streets)
        {
            return street.Key;
        }

        return null;
    }

    private bool OnStreet(int x, int y) =>
        (_rows.TryGetValue(y, out var row) && row.Covers(x)) ||
        (_columns.TryGetValue(x, out var column) && column.Covers(y));

    // Open water beside the city, nearest a top corner first, like the
    // title box of a printed plan
    private bool PlaceCartouche()
    {
        var candidates = new List<(int X, int Y)>
        {
            (Width - CartoucheWidth - 2, 0),
            (1, 0),
            (1, Height - CartoucheHeight),
            (Width - CartoucheWidth - 2, Height - CartoucheHeight),
        };

        for (var y = 0; y + CartoucheHeight <= Height; y++)
        {
            for (var x = Width - CartoucheWidth - 2; x >= 1; x--)
            {
                candidates.Add((x, y));
            }
        }

        foreach (var (x, y) in candidates)
        {
            if (!Free(x - 1, y, CartoucheWidth + 2, CartoucheHeight, water: true)) continue;

            _cartouche = (x, y);
            Occupy(x - 1, y - 1, CartoucheWidth + 2, CartoucheHeight + 2);

            return true;
        }

        return false;
    }

    private void PlaceMapKey()
    {
        PlaceStreetName();

        foreach (var place in AlexandriaMap.Labels)
        {
            var (x, y) = ToCell(place.U, place.V);
            var spaced = string.Join(' ', place.Text.ToCharArray());

            // Letter-spaced like the water names of an old map when it fits
            if (!TryLabel(spaced, x, y, place.OnWater))
            {
                TryLabel(place.Text, x, y, place.OnWater);
            }
        }

        foreach (var (x, y) in new[]
                 {
                     (Width - CompassWidth - 1, 0),
                     (Width - CompassWidth - 1, Height - CompassHeight),
                     (1, Height - CompassHeight),
                     (1, 0),
                 })
        {
            if (!Free(x, y, CompassWidth, CompassHeight, water: true)) continue;

            _compass = (x, y);
            Occupy(x, y, CompassWidth, CompassHeight);

            break;
        }

        var length = Math.Max(4, (int)Math.Round(AlexandriaMap.UnitsPerKilometre / _unitsPerColumn) + 1);
        var span = length + " 1 km".Length;

        foreach (var x in new[] { 1, Width - span - 1 })
        {
            if (!Free(x, Height - 1, span, 1, water: null)) continue;

            _scaleBar = (x, Height - 1, length);
            Occupy(x, Height - 1, span, 1);

            break;
        }
    }

    // The avenue's name sits on the avenue itself, west of the crossing
    private void PlaceStreetName()
    {
        const string name = " CANOPIC WAY ";

        if (!_rows.TryGetValue(_crossY, out var canopic)) return;

        var stretch = _crossX - canopic.From;

        if (stretch < name.Length + 4) return;

        _labels.Add(new PlacedLabel
        {
            Text = name,
            X = canopic.From + (stretch - name.Length) / 2,
            Y = _crossY,
            OnStreet = true,
        });
    }

    // A name sits centred on its anchor, or one row above or below it
    private bool TryLabel(string text, int x, int y, bool onWater)
    {
        var left = x - text.Length / 2;

        foreach (var row in new[] { y, y - 1, y + 1 })
        {
            if (!Free(left - 1, row, text.Length + 2, 1, onWater)) continue;

            _labels.Add(new PlacedLabel { Text = text, X = left, Y = row });
            Occupy(left - 1, row, text.Length + 2, 1);

            return true;
        }

        return false;
    }

    // Every cell in range and unused; all water or all land when asked
    private bool Free(int x, int y, int width, int height, bool? water)
    {
        if (x < 0 || y < 0 || x + width > Width || y + height > Height) return false;

        for (var row = y; row < y + height; row++)
        {
            for (var column = x; column < x + width; column++)
            {
                var index = row * Width + column;

                if (_occupied[index]) return false;
                if (water is { } wantWater && _land[index] == wantWater) return false;
            }
        }

        return true;
    }

    private void Occupy(int x, int y, int width, int height)
    {
        for (var row = Math.Max(0, y); row < Math.Min(Height, y + height); row++)
        {
            for (var column = Math.Max(0, x); column < Math.Min(Width, x + width); column++)
            {
                _occupied[row * Width + column] = true;
            }
        }
    }

    private double CanopicTime(double x) => CrossingTime + AcrossTime * Math.Abs(x - (_crossX + 0.5)) / _canopicReach;

    private double SomaTime(double y) => CrossingTime + AvenueRowTime * Math.Abs(y - (_crossY + 0.5));

    // A street grows from where it meets the avenue it branches off
    private double RowTime(int y, StreetCells row, double x)
    {
        if (row.Major) return CanopicTime(x);

        var origin = Math.Clamp(_crossX, row.From, row.To) + 0.5;

        return SomaTime(y + 0.5) + AcrossTime * Math.Abs(x - origin) / (Width / 2.0);
    }

    private double ColumnTime(int x, StreetCells column, double y)
    {
        var origin = Math.Clamp(_crossY, column.From, column.To) + 0.5;

        return CanopicTime(x + 0.5) + StreetRowTime * Math.Abs(y - origin);
    }

    private static CityEdge Major(double reveal) => new() { Weight = BoxGlyphs.Heavy, Reveal = reveal, Major = true };

    private static CityEdge Minor(double reveal, bool dashed) =>
        new() { Weight = BoxGlyphs.Light, Reveal = reveal, Major = false, Dashed = dashed };

    private static int Mod(int value, int modulus) => (value % modulus + modulus) % modulus;
}
