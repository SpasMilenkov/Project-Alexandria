using Builder.Models;
using Builder.Services;
using Spectre.Console;

namespace Builder.UI.Shell.Screens;

public static class ModelSourceScreen
{
    public static ScreenOutcome Run(Shell shell)
    {
        var config = shell.Context.Config;
        var capabilities = ServiceCatalog.Resolve(config.Features);
        if (!capabilities.AudioAnalysisEnabled) return ScreenOutcome.Continue;

        var service = new ModelPreparationService();
        var plan = service.CreatePlan(config.SourceRoot, config.InstallPath, capabilities.AudioBackbone,
            config.ModelSourceDirectory);

        var choice = Widgets.AskSingleSelect(shell, new Rows(
                Views.Title("Audio analysis models"),
                Views.Note($"{capabilities.AudioBackbone}: {plan.Assets.Count} files, {plan.ExpectedBytes / (1024.0 * 1024):0.##} MB total."),
                Views.Note("Verified files already in this installation's models folder are reused."),
                Views.Note("Downloads use HTTPS from essentia.upf.edu with pinned sizes and SHA-256 checksums. " +
                           "They start during installation, after your final review.")),
            "How should setup obtain the models?",
            new[] { "Existing local files (offline)", "Download missing or corrupt models (HTTPS)" },
            value => value, config.AllowModelDownloads ? 1 : 0);

        if (choice is null)
        {
            shell.TakeEscape();
            return ScreenOutcome.Back;
        }

        if (choice == "Download missing or corrupt models (HTTPS)")
        {
            config.AllowModelDownloads = true;
            return ScreenOutcome.Continue;
        }

        var initial = config.ModelSourceDirectory ?? plan.SourceDirectory;
        var source = Widgets.AskText(shell, new Rows(
                Views.Title("Audio analysis models"),
                Views.Note($"{capabilities.AudioBackbone}: {plan.Assets.Count} files, {plan.ExpectedBytes / (1024.0 * 1024):0.##} MB. " +
                           "Setup copies and verifies existing local models with downloads disabled."),
                Views.Note("Verified files already in this installation's models folder are reused.")),
            new TextFieldSpec
            {
                Label = "Existing model folder",
                Initial = initial,
                Hint = "The folder containing the model files. Checksums come from the source checkout.",
                Validator = value =>
                {
                    if (!Workflow.PathResolver.TryResolve(value, shell.Context.LaunchDirectory, out var resolved, out var error)) return error;

                    var missing = plan.Assets.Where(asset => !File.Exists(Path.Combine(resolved, asset.FileName)) &&
                        !File.Exists(Path.Combine(plan.ModelDirectory, asset.FileName))).ToArray();

                    return missing.Length == 0 ? null : $"Missing {missing.Length} selected model files. Choose an existing cache, go back to enable downloads, or turn audio analysis off.";
                },
            });

        if (shell.TakeEscape()) return ScreenOutcome.Back;

        Workflow.PathResolver.TryResolve(source, shell.Context.LaunchDirectory, out var sourceDirectory, out _);
        config.ModelSourceDirectory = sourceDirectory;
        config.AllowModelDownloads = false;

        return ScreenOutcome.Continue;
    }
}
