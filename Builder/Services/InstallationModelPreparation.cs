using Builder.Models;

namespace Builder.Services;

public interface IInstallationModelPreparation
{
    void Prepare(InstallationConfig config, Action<string> output, CancellationToken cancellationToken);
}

public sealed class InstallationModelPreparation : IInstallationModelPreparation
{
    private readonly HttpClient? _httpClient;

    public InstallationModelPreparation() { }

    internal InstallationModelPreparation(HttpClient httpClient) => _httpClient = httpClient;

    public void Prepare(InstallationConfig config, Action<string> output, CancellationToken cancellationToken)
    {
        var capabilities = ServiceCatalog.Resolve(config.Features);
        if (!capabilities.AudioAnalysisEnabled) return;

        var service = new ModelPreparationService(_httpClient, config.AllowModelDownloads);
        var plan = service.CreatePlan(config.SourceRoot, config.InstallPath,
            capabilities.AudioBackbone, config.ModelSourceDirectory);
        output($"{capabilities.AudioBackbone}: {plan.Assets.Count} files, {plan.ExpectedBytes / (1024.0 * 1024):0.##} MB; " +
               (config.AllowModelDownloads
                   ? "verified local assets are reused; missing or corrupt assets may download from essentia.upf.edu."
                   : "offline preparation; model downloads are disabled."));

        var result = service.PrepareAsync(plan, progress =>
            output($"{progress.Stage}: {progress.FileIndex}/{progress.FileCount} {progress.FileName}; " +
                   $"{progress.CompletedBytes / (1024.0 * 1024):0.##}/{progress.ExpectedBytes / (1024.0 * 1024):0.##} MB"),
            cancellationToken).GetAwaiter().GetResult();
        output($"Verified {result.VerifiedBytes / (1024.0 * 1024):0.##} MB; reused {result.ReusedFiles} files, prepared {result.PreparedFiles}.");
    }
}
