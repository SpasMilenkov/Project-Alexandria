using System.Diagnostics;
using Builder.Models;
using Builder.Services;

namespace Builder.Tests.Integration;

internal sealed class CachedRecoveryDockerService(CachedHttpsFixture fixture) : IDockerService
{
    private readonly DockerService _docker = new();
    private readonly ProcessRunner _runner = new();
    private CancellationToken _cancellationToken;

    public int CachedImageChecks { get; private set; }

    public void Configure(InstallationConfig config, CancellationToken cancellationToken = default)
    {
        CachedHttpsFixture.Require(config.ProjectName == fixture.Context.Config.ProjectName && config.InstallPath == fixture.Target,
            "The recovery adapter may only operate on its disposable HTTPS installation.");
        _cancellationToken = cancellationToken;
        _docker.Configure(config, cancellationToken);
    }

    public bool ComposeBuild(string target, Action<string>? onOutput = null)
    {
        RequireTarget(target);
        var rendered = fixture.Compose(["config", "--images"], ct: _cancellationToken);
        fixture.RequireSuccess(rendered, "read the cached recovery image inventory");
        var images = rendered.StandardOutput.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Distinct(StringComparer.Ordinal).ToArray();
        CachedHttpsFixture.Require(images.Length > 0, "The cached recovery image inventory is empty.");

        foreach (var image in images)
        {
            fixture.RequireSuccess(QueryDocker(["image", "inspect", "--format", "{{.Id}}", image]),
                "verify a cached recovery image");
        }

        CachedImageChecks++;
        onOutput?.Invoke("Verified cached runtime images; current application outputs are mounted. This acceptance test does not build source images.");

        return true;
    }

    public bool ComposeUpCore(string target, Action<string>? onOutput = null) =>
        Up(target, CachedInfrastructureFixture.CoreServices, onOutput);

    public bool ComposeUpApi(string target, Action<string>? onOutput = null) => Up(target, ["api"], onOutput);

    public bool ComposeUpAll(string target, Action<string>? onOutput = null) => Up(target, [], onOutput);

    public bool ComposeDown(string target) =>
        throw new InvalidOperationException("Repair must preserve the running fixture. Only its disposal owns cleanup.");

    public bool ValidateConfiguration(string target, Action<string>? onOutput = null) => _docker.ValidateConfiguration(target, onOutput);

    public IReadOnlyList<string> GetExpectedServices(string target, Action<string>? onOutput = null) => _docker.GetExpectedServices(target, onOutput);

    public bool IsHttpsPortOwned(string target, int port) => _docker.IsHttpsPortOwned(target, port);

    public void VerifyInstallationOwnership(string target, InstallationConfig config, Action<string>? onOutput = null) =>
        _docker.VerifyInstallationOwnership(target, config, onOutput);

    public IReadOnlyList<ContainerStatus> GetServiceStatuses(string target, Action<string>? onOutput = null) =>
        _docker.GetServiceStatuses(target, onOutput);

    public List<string> GetContainerStatuses(string target, Action<string>? onOutput = null) => _docker.GetContainerStatuses(target, onOutput);

    public bool WaitForHealthy(string target, TimeSpan timeout, IReadOnlyCollection<string> oneShotContainers,
        Action<IReadOnlyList<string>>? onPoll = null) => _docker.WaitForHealthy(target, timeout, oneShotContainers, onPoll);

    public bool WaitForServices(string target, IReadOnlyCollection<string> expectedServices, TimeSpan timeout,
        IReadOnlyCollection<string> oneShotServices, Action<IReadOnlyList<ContainerStatus>>? onPoll = null,
        Action<string>? onOutput = null) => _docker.WaitForServices(target, expectedServices, timeout, oneShotServices, onPoll, onOutput);

    public bool WaitForMonitoring(string target, TimeSpan timeout, Action<string>? onOutput = null) => _docker.WaitForMonitoring(target, timeout, onOutput);

    public ProcessResult QueryDocker(IReadOnlyList<string> arguments)
    {
        var start = new ProcessStartInfo("docker")
        {
            WorkingDirectory = fixture.Context.SourceRoot,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };

        foreach (var argument in arguments) start.ArgumentList.Add(argument);

        return _runner.Run(start, TimeSpan.FromSeconds(30), _cancellationToken);
    }

    private bool Up(string target, IReadOnlyList<string> services, Action<string>? onOutput)
    {
        RequireTarget(target);
        var result = fixture.Compose(["up", "--detach", "--pull", "never", "--no-build", .. services],
            TimeSpan.FromMinutes(3), _cancellationToken);
        fixture.RequireSuccess(result, "resume disposable services with cached images");
        onOutput?.Invoke("Started the disposable installation with --pull never --no-build.");

        return true;
    }

    private void RequireTarget(string target) => CachedHttpsFixture.Require(target == fixture.Target,
        "The recovery adapter cannot operate outside its disposable installation.");
}
