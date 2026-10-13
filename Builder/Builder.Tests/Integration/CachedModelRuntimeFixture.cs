using System.Diagnostics;
using Builder.Services;
using Builder.Tests.Services;

namespace Builder.Tests.Integration;

internal sealed class CachedModelRuntimeFixture : IDisposable
{
    private readonly ProcessRunner _runner = new();
    private readonly string _identity = Guid.NewGuid().ToString("N");
    private readonly string _target = Path.Combine(Path.GetTempPath(), "builder-cached-models-" + Guid.NewGuid().ToString("N"));
    private readonly CancellationToken _ct = TestContext.Current.CancellationToken;
    private bool _attempted;

    public string VerifyBothBackbones()
    {
        var modelSource = Environment.GetEnvironmentVariable("ALEXANDRIA_EXISTING_MODEL_SOURCE");

        if (string.IsNullOrWhiteSpace(modelSource) || !Directory.Exists(modelSource))
        {
            throw new InvalidOperationException("Set ALEXANDRIA_EXISTING_MODEL_SOURCE to an existing cached model directory. This acceptance test never downloads models or images.");
        }

        var source = ConfigurationArtifactFixture.FindRepositoryRoot();
        var start = Start("python3", ["-m", "unittest", "discover", "-s", Path.Combine(source, "classificators", "essentia", "tests"),
            "-p", "test_cached_runtime.py", "-v"]);
        start.WorkingDirectory = source;
        start.Environment["ALEXANDRIA_BUILDER_MODEL_RUNTIME_TESTS"] = "1";
        start.Environment["ALEXANDRIA_EXISTING_MODEL_SOURCE"] = Path.GetFullPath(modelSource);
        start.Environment["ALEXANDRIA_MODEL_RUNTIME_IDENTITY"] = _identity;
        start.Environment["ALEXANDRIA_MODEL_RUNTIME_TARGET"] = _target;
        _attempted = true;
        var result = _runner.Run(start, TimeSpan.FromMinutes(15), _ct);

        if (!result.Succeeded)
        {
            throw new InvalidOperationException("Current cached-model inference/UID acceptance failed: " + result.StandardError);
        }

        return result.StandardOutput;
    }

    private static ProcessStartInfo Start(string command, IEnumerable<string> arguments)
    {
        var start = new ProcessStartInfo(command) { RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false };
        foreach (var argument in arguments) start.ArgumentList.Add(argument);
        return start;
    }

    public void Dispose()
    {
        if (!_attempted) return;
        var containers = _runner.Run(Start("docker", ["ps", "--all", "--quiet", "--filter", "label=alexandria.fixture.model-runtime=" + _identity]),
            TimeSpan.FromSeconds(20), CancellationToken.None);

        foreach (var id in containers.StandardOutput.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            _runner.Run(Start("docker", ["rm", "--force", id]), TimeSpan.FromSeconds(20), CancellationToken.None);
        }

        var volume = "builder-model-runtime-" + _identity;
        var inspect = _runner.Run(Start("docker", ["volume", "inspect", volume]), TimeSpan.FromSeconds(10), CancellationToken.None);

        if (inspect.Succeeded)
        {
            var removed = _runner.Run(Start("docker", ["volume", "rm", volume]), TimeSpan.FromSeconds(20), CancellationToken.None);
            if (!removed.Succeeded) throw new InvalidOperationException("The disposable model-runtime volume could not be removed: " + removed.StandardError);
        }

        if (Directory.Exists(_target)) Directory.Delete(_target, recursive: true);
    }
}
