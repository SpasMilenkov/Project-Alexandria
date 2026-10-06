using Builder.Models;
using Builder.Services;
using Spectre.Console;
using Spectre.Console.Rendering;

namespace Builder.UI.Shell.Screens;

public static class FeaturesScreen
{
    public static ScreenOutcome Run(Shell shell, IResourceCalculator calculator)
    {
        var features = FeatureSelection.FromDefaults();

        shell.Context.Features = features;

        IRenderable Header() => Views.Title("What should Alexandria be able to do?");

        // Sidebar stats react to every toggle between keystrokes (D36)
        void PushStats()
        {
            shell.UpdateSidebarStats(stats =>
            {
                stats.Clear();
                stats.Add(("RAM estimate", $"{calculator.CalculateMinimumRamMb(features) / 1024.0:0.#} GB"));
                stats.Add(("Services", $"{calculator.CountServices(features)} will run"));
            });
        }

        PushStats();

        Widgets.AskMultiToggle(
            shell,
            Header(),
            "Features - Space toggles, Enter confirms",
            FeatureCatalog.All,
            f => features.IsEnabled(f.Id),
            (f, on) =>
            {
                features.SetEnabled(f.Id, on);
                PushStats();
            },
            f => f.Label,
            detailFor: index =>
            {
                var feature = FeatureCatalog.All[index];
                var components = ServiceCatalog.ForFeature(feature.Id).ToList();

                var rows = new List<IRenderable>
                {
                    new Markup($"[bold]{Markup.Escape(feature.Label)}[/]"),
                    new Markup(Markup.Escape(feature.Description)),
                    new Markup($"[dim]{Markup.Escape(feature.Cost)}[/]"),
                };

                if (components.Count > 0)
                {
                    rows.Add(new Markup($"[dim]Turns on: [/]" +
                        Markup.Escape(string.Join(", ", components.Select(c => c.Name)))));
                }

                return new Rows(rows.ToArray());
            });

        if (shell.TakeEscape())
        {
            return ScreenOutcome.Back;
        }

        shell.Context.FeaturesSelected = true;
        shell.Context.Config.Features = features;

        return ScreenOutcome.Continue;
    }
}
