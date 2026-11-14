using Builder.Models;
using Spectre.Console;
using Spectre.Console.Rendering;

namespace Builder.UI.Shell.Screens;

// The planned Garage layout drawn against Docker's free disk, compared in
// bytes so the capacity's GB and the measured free space line up exactly
internal static class StorageFit
{
    private const int MaxBarWidth = 64;

    // Past this share of the free disk, images, previews and backups have
    // little room left
    private const double ComfortableShare = 0.8;

    public static IRenderable Build(long plannedGb, long? freeMb)
    {
        if (freeMb is not { } free)
        {
            return new Markup($"[bold]{plannedGb} GB[/] {Ui.Muted("planned · Docker's free disk could not be measured")}");
        }

        var plannedBytes = (double)plannedGb * StorageSettings.BytesPerGb;
        var freeBytes = (double)free * 1024 * 1024;
        var width = Math.Min(Shell.MainContentWidth, MaxBarWidth);
        var scale = Math.Max(1, Math.Max(plannedBytes, freeBytes));
        var filled = Math.Clamp((int)Math.Ceiling(width * plannedBytes / scale), 1, width);
        var (color, remark) = Judge(plannedBytes, freeBytes);

        var bar = $"[{color}]{new string('█', filled)}[/][{Theme.Active.Bo}]{new string('░', width - filled)}[/]";
        var summary = $"[bold]{plannedGb} GB[/] {Ui.Muted("planned ·")} {Ui.Muted($"{free / 1024.0:0} GB free for Docker")}";

        if (remark is not null)
        {
            summary += $" {Ui.Muted("·")} [{color}]{Markup.Escape(remark)}[/]";
        }

        return new Rows(new Markup(bar), new Markup(summary));
    }

    private static (string Color, string? Remark) Judge(double plannedBytes, double freeBytes)
    {
        if (plannedBytes > freeBytes)
        {
            return (Theme.Active.Er, "more than Docker has free");
        }

        if (plannedBytes > freeBytes * ComfortableShare)
        {
            return (Theme.Active.Wa, "leaves little room for images and backups");
        }

        return (Theme.Active.As, null);
    }
}
