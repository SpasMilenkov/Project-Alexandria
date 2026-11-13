using Builder.Models;
using Spectre.Console;
using Spectre.Console.Rendering;

namespace Builder.UI.Shell.Screens;

// The selection's memory estimate as one bar split by service group, drawn
// against this computer's memory when it is known. The feature under the
// cursor stands out; one that is off shows what it would add, so the cost of
// a choice is visible before it is made.
internal static class MemoryBudget
{
    private const int MaxBarWidth = 64;
    private const string Separator = "   ";

    // Memory is colored by the feature that brings it in, so a feature's
    // groups share a color and the always-running core stays neutral
    private static readonly string[] Groups = ServiceCatalog.All.Select(service => service.Group).Distinct().ToArray();
    private static readonly string?[] GroupOwners = Groups.Select(Owner).ToArray();
    private static readonly string[] Owners = GroupOwners.OfType<string>().Distinct().ToArray();


    // A note explains a highlighted share that the toggle does not change
    public static IRenderable Build(IReadOnlyList<MemorySegment> segments, long? machineMb, string featureLabel, string? note = null)
    {
        var width = Math.Min(Shell.MainContentWidth, MaxBarWidth);
        var usedMb = segments.Where(segment => !segment.Preview).Sum(segment => segment.MemoryMb);
        var previewMb = segments.Where(segment => segment.Preview).Sum(segment => segment.MemoryMb);
        var scaleMb = Math.Max(1, Math.Max(usedMb + previewMb, machineMb ?? 0));

        return new Rows(
            new Markup(Bar(segments, width, scaleMb, machineMb)),
            new Markup(Summary(usedMb, previewMb, machineMb, featureLabel, note)),
            new Markup(Legend(segments, width)));
    }

    private static string Bar(IReadOnlyList<MemorySegment> segments, int width, long scaleMb, long? machineMb)
    {
        // Past this cell the selection no longer fits the computer's memory
        var limit = machineMb is { } machine ? (int)Math.Round(width * machine / (double)scaleMb) : width;
        var cells = new List<(char Glyph, string Color)>();
        var totalMb = 0L;

        foreach (var segment in segments)
        {
            totalMb += segment.MemoryMb;

            var end = Math.Max(cells.Count + 1, (int)Math.Round(width * totalMb / (double)scaleMb));

            while (cells.Count < Math.Min(end, width))
            {
                var over = !segment.Preview && cells.Count >= limit;
                var glyph = Glyph(segment);

                cells.Add((glyph, over ? Theme.Active.Er : segment.Color.ToMarkup()));
            }
        }

        // The memory left over on this computer
        while (machineMb is not null && cells.Count < width)
        {
            cells.Add(('░', Theme.Active.Bo));
        }

        var markup = new System.Text.StringBuilder();

        foreach (var run in Runs(cells))
        {
            markup.Append($"[{run.Color}]{run.Text}[/]");
        }

        return markup.ToString();
    }

    private static char Glyph(MemorySegment segment)
    {
        if (segment.Preview) return '▒';

        return segment.Focused ? '█' : '▓';
    }

    private static IEnumerable<(string Text, string Color)> Runs(List<(char Glyph, string Color)> cells)
    {
        var start = 0;

        for (var i = 1; i <= cells.Count; i++)
        {
            if (i < cells.Count && cells[i] == cells[start]) continue;

            yield return (new string(cells[start].Glyph, i - start), cells[start].Color);
            start = i;
        }
    }

    private static string Summary(long usedMb, long previewMb, long? machineMb, string featureLabel, string? note)
    {
        var text = $"[bold]{Gigabytes(usedMb)}[/] {Ui.Muted("estimated")}";

        if (machineMb is { } machine)
        {
            text += usedMb > machine
                ? $" {Ui.Muted("·")} {Ui.Warning($"more than this computer's {Gigabytes(machine)}")}"
                : $" {Ui.Muted($"· this computer has {Gigabytes(machine)}")}";
        }

        if (previewMb > 0)
        {
            text += $" {Ui.Muted($"· +{Gigabytes(previewMb)} with {featureLabel}")}";
        }

        if (note is not null)
        {
            text += $" {Ui.Muted("· " + note)}";
        }

        return text;
    }

    // Wrapped by hand so an entry never splits between its swatch and label
    private static string Legend(IReadOnlyList<MemorySegment> segments, int width)
    {
        var lines = new List<string> { string.Empty };
        var lineWidth = 0;

        foreach (var segment in segments)
        {
            var amount = segment.Preview ? "+" + Gigabytes(segment.MemoryMb) : Gigabytes(segment.MemoryMb);
            var plain = $"■ {segment.Label} {amount}";
            var label = segment.Focused || segment.Preview ? $"[bold]{Markup.Escape(segment.Label)}[/]" : Ui.Muted(segment.Label);
            var entry = $"[{segment.Color.ToMarkup()}]{Glyph(segment)}[/] {label} {Ui.Muted(amount)}";

            if (lineWidth > 0 && lineWidth + Separator.Length + plain.Length > width)
            {
                lines.Add(string.Empty);
                lineWidth = 0;
            }

            if (lineWidth > 0)
            {
                lines[^1] += Separator;
                lineWidth += Separator.Length;
            }

            lines[^1] += entry;
            lineWidth += plain.Length;
        }

        return string.Join("\n", lines);
    }

    private static string Gigabytes(long megabytes) => $"{megabytes / 1024.0:0.#} GB";

    // The same group keeps the same color on every screen
    public static Color GroupColor(string group)
    {
        var index = Array.IndexOf(Groups, group);

        if (index < 0 || GroupOwners[index] is not { } owner) return Theme.Active.MutedColor;

        var colors = Theme.Active.ChartColors;

        return colors[Array.IndexOf(Owners, owner) % colors.Count];
    }

    // The toggle that every service in the group depends on, if there is one
    private static string? Owner(string group)
    {
        var services = ServiceCatalog.All.Where(service => service.Group == group).ToList();

        return FeatureCatalog.ToggleFeatures
            .FirstOrDefault(feature => services.All(service =>
                service.RequiresFeatures.Contains(feature.Id) || service.RequiresAllFeatures.Contains(feature.Id)))
            ?.Id;
    }
}
