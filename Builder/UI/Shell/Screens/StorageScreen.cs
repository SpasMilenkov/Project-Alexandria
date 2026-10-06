using System.Globalization;
using Builder.Models;
using Builder.Services;
using Spectre.Console;
using Spectre.Console.Rendering;

namespace Builder.UI.Shell.Screens;

public static class StorageScreen
{
    public static ScreenOutcome Run(Shell shell)
    {
        var context = shell.Context;
        var settings = context.Config.Storage;

        if (context.IsRepair || settings.PreserveExistingCapacity)
        {
            settings.PreserveExistingCapacity = true;
            shell.SetMain(new Rows(new IRenderable[] { Views.Title("Storage for your files") }
                .Concat(StorageSummary.Describe(context.Config, context.Resources).Select(Views.Note)).ToArray()));
            return IntroScreen.WaitEnter(shell) ? ScreenOutcome.Back : ScreenOutcome.Continue;
        }

        var raw = Widgets.AskText(shell, new Rows(
            Views.Title("Storage for your files"),
            Views.Note("Set the new Garage storage layout capacity. The initial default is 10 GB."),
            Views.Note("This does not reserve disk space or set user quotas. Keep room for the database, previews, temporary processing and Docker images.")),
            new TextFieldSpec
            {
                Label = "Garage capacity in GB",
                Initial = settings.GarageCapacityGb.ToString(CultureInfo.InvariantCulture),
                Hint = "Whole decimal GB, at least 1. Existing storage layouts are preserved on repair.",
                Validator = value => long.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var gb)
                    && gb >= 1 && gb <= long.MaxValue / StorageSettings.BytesPerGb ? null : "Enter a whole number of GB, at least 1.",
            });

        if (shell.TakeEscape()) return ScreenOutcome.Back;
        settings.GarageCapacityGb = long.Parse(raw, CultureInfo.InvariantCulture);
        settings.CapacityReviewed = true;
        return ScreenOutcome.Continue;
    }
}
