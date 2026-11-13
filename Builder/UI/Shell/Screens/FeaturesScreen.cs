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

        IRenderable Header() => Views.Heading("What should Alexandria be able to do?",
            "Everything can be changed later by running setup again on the same folder.");

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
                "Features",
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

                    var body = new List<string>
                    {
                        Markup.Escape(feature.Description),
                        Ui.Muted(feature.Cost),
                    };

                    if (components.Count > 0)
                    {
                        var verb = components.All(component => component.AlwaysEnabled) ? "Uses: " : "Turns on: ";
                        var memoryMb = components.Where(component => !component.OneShot)
                            .Sum(component => component.MemoryMb + component.MemoryHeadroomMb);

                        body.Add(Ui.Muted(verb) + Markup.Escape(string.Join(", ", components.Select(c => c.Name))));
                        body.Add(Ui.Muted($"Components estimate: {memoryMb / 1024.0:0.##} GB of memory, including headroom."));
                    }

                    return Views.InfoPanel(feature.Label, string.Join("\n", body));
                });

            if (shell.TakeEscape())
            {
                return ScreenOutcome.Back;
            }

            var backboneHeader = new List<IRenderable>
            {
                Views.Heading("Which audio analysis model?",
                    "Automatic tagging reads genre and mood from your music with one of these models."),
            };

            if (features.HasLegacyDualAudioSelection)
            {
                backboneHeader.Add(Views.WarningPanel("This saved setup selected both models. Its coordinator used Effnet. Confirm one model to continue."));
            }

            if (!features.IsEnabled(FeatureCatalog.Autotagging.Id))
            {
                backboneHeader.Add(Views.InfoPanel("Automatic tagging is off",
                    Markup.Escape("Your model choice is saved, but no analysis worker or models will be installed.")));
            }

            var choices = new[] { "off", "effnet", "maest" };
            var backbone = Widgets.AskSingleSelect(shell, new Rows(backboneHeader), "Audio analysis model", choices,
                choice => choice switch
                {
                    "effnet" => "Effnet - genre and mood",
                    "maest" => "MAEST - deeper genre analysis plus mood",
                    _ => "Off",
                }, Array.IndexOf(choices, features.AudioBackbone),
                choice => Views.Note(choice switch
                {
                    "effnet" => "About 23 MB of models and 2 GB of worker memory. The lighter choice.",
                    "maest" => "About 353 MB of models and 2 GB of worker memory. Slower, with finer genre labels.",
                    _ => "No analysis worker or models are installed. Descriptive media metadata remains available.",
                }));

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
