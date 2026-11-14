using System.Globalization;
using Builder.Models;
using Builder.Services;
using Spectre.Console.Rendering;

namespace Builder.UI.Shell.Screens;

public static class StorageScreen
{
    private const string Title = "Storage for your files";

    public static ScreenOutcome Run(Shell shell)
    {
        var context = shell.Context;
        var settings = context.Config.Storage;

        if (context.IsRepair || settings.PreserveExistingCapacity)
        {
            settings.PreserveExistingCapacity = true;
            shell.SetMain(() => new Rows(
                Views.Heading(Title, "Setup keeps your existing storage layout."),
                Details(context.Config, context.Resources, includeLayout: true)));

            return Widgets.WaitForEnter(shell) ? ScreenOutcome.Back : ScreenOutcome.Continue;
        }

        var raw = Widgets.AskText(shell, Views.Heading(Title,
                "How much room Garage, the file store, plans its layout around."),
            new TextFieldSpec
            {
                Label = "Garage capacity in GB",
                Initial = settings.GarageCapacityGb.ToString(CultureInfo.InvariantCulture),
                Hint = "Whole GB, at least 1.",
                Validator = value => TryParseCapacity(value, out _) ? null : "Enter a whole number of GB, at least 1.",
                Preview = value => new Rows(
                    Fit(value, context.Resources),
                    Views.Blank(),
                    Details(context.Config, context.Resources, includeLayout: false)),
            });

        if (shell.TakeEscape()) return ScreenOutcome.Back;

        settings.GarageCapacityGb = long.Parse(raw, CultureInfo.InvariantCulture);
        settings.CapacityReviewed = true;

        return ScreenOutcome.Continue;
    }

    // Half-typed or invalid input keeps the bar's place with a prompt, so the
    // page does not jump while the number is being edited
    private static IRenderable Fit(string value, SystemResources resources)
    {
        if (!TryParseCapacity(value, out var gb))
        {
            return new Rows(Views.Note("Enter a whole number to see how it fits."), Views.Blank());
        }

        return StorageFit.Build(gb, resources.DockerAvailableDiskMb);
    }

    private static IRenderable Details(InstallationConfig config, SystemResources resources, bool includeLayout)
    {
        var overview = StorageSummary.Describe(config, resources);
        var facts = includeLayout ? overview.Facts.Prepend(("Garage layout", overview.Layout)) : overview.Facts;

        return new Rows(
            Views.Section("Where your data lives"),
            Views.Facts(facts),
            Views.Blank(),
            Views.InfoPointsPanel("Good to know", overview.Points));
    }

    private static bool TryParseCapacity(string value, out long gb)
    {
        return long.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out gb)
               && gb >= 1 && gb <= long.MaxValue / StorageSettings.BytesPerGb;
    }
}
