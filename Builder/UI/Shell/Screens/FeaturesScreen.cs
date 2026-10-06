using Builder.Models;
using Builder.Services;
using Spectre.Console;
using Spectre.Console.Rendering;

namespace Builder.UI.Shell.Screens;

public static class FeaturesScreen
{
    public static ScreenOutcome Run(Shell shell, IResourceCalculator calculator)
    {
        var features = shell.Context.Features;

        IRenderable Header() => Views.Title("What should Alexandria be able to do?");

        void PushStats()
        {
            var capabilities = ServiceCatalog.Resolve(features);

            shell.UpdateSidebarStats(stats =>
            {
                stats.Clear();
                stats.Add(("RAM estimate", $"{calculator.CalculateMinimumRamMb(features) / 1024.0:0.#} GB"));
                stats.Add(("Services", $"{capabilities.RunningServiceCount} ongoing"));
                stats.Add(("Setup jobs", $"{capabilities.OneShotServiceIds.Count} run once"));
            });
        }

        PushStats();

        while (true)
        {
            Widgets.AskMultiToggle(
                shell,
                Header(),
                "Features - Space toggles, Enter confirms",
                FeatureCatalog.ToggleFeatures,
                f => features.IsEnabled(f.Id),
                (f, on) =>
                {
                    features.SetEnabled(f.Id, on);
                    PushStats();
                },
                f => f.Label,
                detailFor: index =>
                {
                    var feature = FeatureCatalog.ToggleFeatures[index];
                    var components = ServiceCatalog.ForFeature(feature.Id).ToList();

                    var rows = new List<IRenderable>
                    {
                        new Markup($"[bold]{Markup.Escape(feature.Label)}[/]"),
                        new Markup(Markup.Escape(feature.Description)),
                        new Markup($"[dim]{Markup.Escape(feature.Cost)}[/]"),
                    };

                    if (components.Count > 0)
                    {
                        rows.Add(new Markup($"[dim]{(components.All(component => component.AlwaysEnabled) ? "Uses: " : "Turns on: ")}[/]" +
                            Markup.Escape(string.Join(", ", components.Select(c => c.Name)))));
                        var memoryMb = components.Where(component => !component.OneShot)
                            .Sum(component => component.MemoryMb + component.MemoryHeadroomMb);

                        rows.Add(new Markup($"[dim]Components estimate: {memoryMb / 1024.0:0.##} GB of memory, including headroom.[/]"));
                    }

                    return new Rows(rows.ToArray());
                });

            if (shell.TakeEscape())
            {
                return ScreenOutcome.Back;
            }

            var backboneHeader = new List<IRenderable> { Header() };

            if (features.HasLegacyDualAudioSelection)
            {
                backboneHeader.Add(new Markup("[yellow]This saved setup selected both models. Its coordinator used Effnet. Confirm one model to continue.[/]"));
            }

            if (!features.IsEnabled(FeatureCatalog.Autotagging.Id))
            {
                backboneHeader.Add(new Markup("[dim]Automatic tagging is off. Your model choice is saved, but no analysis worker or models will be installed.[/]"));
            }

            var choices = new[] { "off", "effnet", "maest" };
            var backbone = Widgets.AskSingleSelect(shell, new Rows(backboneHeader), "Audio analysis model", choices,
                choice => choice switch
                {
                    "effnet" => "Effnet - genre and mood; about 23 MB of models, 2 GB worker memory",
                    "maest" => "MAEST - deeper genre analysis plus mood; about 353 MB of models, 2 GB worker memory",
                    _ => "Off - descriptive media metadata remains available",
                }, Array.IndexOf(choices, features.AudioBackbone));

            if (shell.TakeEscape()) continue;
            if (backbone is null) continue;

            features.SetAudioBackbone(backbone);
            PushStats();
            break;
        }

        shell.Context.FeaturesSelected = true;
        shell.Context.Config.Features = features;

        return ScreenOutcome.Continue;
    }
}
