using System.Diagnostics;
using Builder.Models;
using Builder.Services;
using Spectre.Console;
using Spectre.Console.Rendering;

namespace Builder.UI.Shell.Screens;

public static class FeaturesScreen
{
    // The computer's memory arrives as a parameter so tests stay independent
    // of the machine running them; null leaves the bar unscaled
    public static ScreenOutcome Run(Shell shell, IResourceCalculator calculator, long? machineMemoryMb = null)
    {
        var features = shell.Context.Features;
        (string Id, bool TurnedOn, long At)? lastToggle = null;

        IRenderable Header() => Views.Heading("What should Alexandria be able to do?",
            "Everything can be changed later by running setup again on the same folder.");

        void PushStats() => SidebarStats.Push(shell, calculator);

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
                    lastToggle = (f.Id, on, Stopwatch.GetTimestamp());
                    PushStats();
                },
                f => f.Label,
                detailFor: index => Detail(features, FeatureCatalog.ToggleFeatures[index], machineMemoryMb, lastToggle));

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

    // The memory bar for the whole selection, then the feature under the
    // cursor with the services it runs
    private static IRenderable Detail(
        FeatureSelection features,
        InstallFeature feature,
        long? machineMemoryMb,
        (string Id, bool TurnedOn, long At)? lastToggle)
    {
        var enabled = features.IsEnabled(feature.Id);
        var added = AddedServices(features, feature.Id);

        // A feature that only changes what an always-running service does
        // adds nothing of its own; it is shown through that service instead,
        // highlighted whether the toggle is on or off since it runs either way
        var alwaysRuns = added.Count == 0;
        var runs = alwaysRuns ? ServiceCatalog.ForFeature(feature.Id).Where(service => !service.OneShot).ToList() : added;
        var focusedGroups = runs.Select(service => service.Group).ToHashSet(StringComparer.Ordinal);

        var segments = ServiceCatalog.Resolve(features).MemoryEstimates
            .Select(estimate => new MemorySegment
            {
                Label = estimate.Label,
                MemoryMb = estimate.MemoryMb,
                Color = MemoryBudget.GroupColor(estimate.Label),
                Focused = (enabled || alwaysRuns) && focusedGroups.Contains(estimate.Label),
            })
            .ToList();

        if (!enabled)
        {
            segments.AddRange(added.GroupBy(service => service.Group).Select(group => new MemorySegment
            {
                Label = group.Key,
                MemoryMb = group.Sum(service => service.MemoryMb + service.MemoryHeadroomMb),
                Color = MemoryBudget.GroupColor(group.Key),
                Preview = true,
            }));
        }

        var card = new List<IRenderable>
        {
            new Markup($"[bold {Theme.Active.As}]● {Markup.Escape(feature.Label)}[/]"),
            new Markup(Markup.Escape(feature.Description)),
            new Markup(Ui.Muted(feature.Cost)),
        };

        if (runs.Count > 0)
        {
            var memoryMb = runs.Sum(service => service.MemoryMb + service.MemoryHeadroomMb);

            card.Add(Views.Blank());
            card.Add(new Markup(Ui.Muted($"{RunsVerb(!alwaysRuns, enabled)} · {memoryMb / 1024.0:0.#} GB of memory with headroom")));
            card.Add(new ServiceChips
            {
                Names = runs.Select(service => service.Name).ToList(),
                Running = enabled || alwaysRuns,
                Toggled = lastToggle is { } toggle && toggle.Id == feature.Id ? (toggle.TurnedOn, toggle.At) : null,
            });
        }

        return new Rows(
            MemoryBudget.Build(segments, machineMemoryMb, feature.Label,
                alwaysRuns && runs.Count > 0 ? "highlighted part runs either way" : null),
            Views.Blank(),
            new Callout(new Rows(card), Style.Parse(Theme.Active.As)),
            Views.Blank());
    }

    private static string RunsVerb(bool addsServices, bool enabled)
    {
        if (!addsServices) return "Runs whether this is on or off";

        return enabled ? "Turns on" : "Would turn on";
    }

    // Exactly what this feature adds to the current selection: the services
    // with it on that are not there with it off
    private static List<ServiceInfo> AddedServices(FeatureSelection features, string featureId)
    {
        var with = new FeatureSelection { Enabled = new HashSet<string>(features.Enabled) };
        var without = new FeatureSelection { Enabled = new HashSet<string>(features.Enabled) };

        with.SetEnabled(featureId, true);
        without.SetEnabled(featureId, false);

        var baseline = ServiceCatalog.Resolve(without).ServiceIds.ToHashSet(StringComparer.Ordinal);

        return ServiceCatalog.Resolve(with).Services
            .Where(service => !service.OneShot && !baseline.Contains(service.Id))
            .ToList();
    }
}
