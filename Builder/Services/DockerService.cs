using System.Diagnostics;
using System.Text.Json;
using System.Text.RegularExpressions;
using Builder.Models;
using Builder.Workflow;

namespace Builder.Services;

public interface IDockerService
{
    bool ComposeBuild(string workingDirectory, Action<string>? onOutput = null);
    bool ComposeUpCore(string workingDirectory, Action<string>? onOutput = null);
    bool ComposeUpAll(string workingDirectory, Action<string>? onOutput = null);
    bool ComposeDown(string workingDirectory);
    bool WaitForHealthy(string workingDirectory, TimeSpan timeout, IReadOnlyCollection<string> oneShotContainers,
        Action<IReadOnlyList<string>>? onPoll = null);
    List<string> GetContainerStatuses(string workingDirectory, Action<string>? onOutput = null);

    void Configure(InstallationConfig config, CancellationToken cancellationToken = default) { }
    void ConfigureDiagnostics(Action<ProcessStartInfo, ProcessResult?>? onCommand) { }
    bool ValidateConfiguration(string target, Action<string>? onOutput = null) => false;
    IReadOnlyList<string> GetExpectedServices(string target, Action<string>? onOutput = null) => [];
    bool WaitForMonitoring(string target, TimeSpan timeout, Action<string>? onOutput = null) => false;
    bool ComposeUpApi(string target, Action<string>? onOutput = null) => false;
    bool IsHttpsPortOwned(string target, int port) => false;
    IReadOnlyList<ContainerStatus> GetServiceStatuses(string target, Action<string>? onOutput = null) => [];
    IReadOnlyList<string> GetServiceLogs(string target, string service, int tail = 100) => [];
    bool WaitForServices(string target, IReadOnlyCollection<string> expectedServices, TimeSpan timeout,
        IReadOnlyCollection<string> oneShotServices, Action<IReadOnlyList<ContainerStatus>>? onPoll = null,
        Action<string>? onOutput = null) => false;
    LegacyInstallationIdentity DiscoverLegacyIdentity(string target, Action<string>? onOutput = null,
        CancellationToken cancellationToken = default) => throw new InvalidOperationException("Docker ownership inspection is unavailable.");
    void VerifyInstallationOwnership(string target, InstallationConfig config, Action<string>? onOutput = null) =>
        throw new InvalidOperationException("Docker ownership inspection is unavailable.");
}

public class DockerService : IDockerService
{
    private readonly string _launchDirectory;
    private readonly IProcessRunner _runner;
    private readonly TimeProvider _timeProvider;
    private string? _projectName;
    private CancellationToken _cancellationToken;
    private InstallationConfig? _config;
    private Action<ProcessStartInfo, ProcessResult?>? _onCommand;

    internal TimeSpan QueryTimeout { get; init; } = TimeSpan.FromSeconds(30);
    internal TimeSpan BuildTimeout { get; init; } = TimeSpan.FromHours(1);
    internal TimeSpan PollInterval { get; init; } = TimeSpan.FromSeconds(2);

    public DockerService(string? launchDirectory = null)
        : this(new ProcessRunner(), launchDirectory)
    {
    }

    public DockerService(IProcessRunner runner, string? launchDirectory = null, TimeProvider? timeProvider = null)
    {
        _runner = runner;
        _timeProvider = timeProvider ?? TimeProvider.System;
        _launchDirectory = Path.GetFullPath(launchDirectory ?? Environment.CurrentDirectory);
    }

    public void Configure(InstallationConfig config, CancellationToken cancellationToken = default)
    {
        if (!Regex.IsMatch(config.ProjectName, "^[a-z0-9][a-z0-9_-]*$", RegexOptions.CultureInvariant))
        {
            throw new InvalidOperationException("The installation needs a valid persisted Compose project name before Docker commands can run.");
        }

        _projectName = config.ProjectName;
        _cancellationToken = cancellationToken;
        _config = config;
    }

    public void ConfigureDiagnostics(Action<ProcessStartInfo, ProcessResult?>? onCommand) => _onCommand = onCommand;

    public bool ComposeBuild(string target, Action<string>? onOutput = null) =>
        RunCompose(target, ["build"], onOutput, BuildTimeout).Succeeded;

    public bool ComposeUpCore(string target, Action<string>? onOutput = null) =>
        RunCompose(target, ["up", "-d", .. ServiceCatalog.All.Where(service => service.Infrastructure).Select(service => service.Id)], onOutput).Succeeded;

    public bool ComposeUpApi(string target, Action<string>? onOutput = null) =>
        RunCompose(target, ["up", "-d", "api"], onOutput).Succeeded;

    public bool ComposeUpAll(string target, Action<string>? onOutput = null)
    {
        if (_config is null) throw new InvalidOperationException("Configure the reviewed installation before starting its services.");

        target = ResolveTarget(target);
        var expected = ServiceCatalog.Resolve(_config.Features).ServiceIds.ToHashSet(StringComparer.Ordinal);
        var actual = GetExpectedServices(target, onOutput);

        if (!expected.SetEquals(actual))
        {
            throw new InvalidOperationException("The Compose service inventory differs from the reviewed feature selection. " +
                                                "Return to the summary before reconciling service containers.");
        }

        var disabled = new DockerOwnershipInspector(arguments => RunQuery(target, arguments, _cancellationToken, onOutput))
            .FindDisabledServiceContainers(target, _config);

        if (disabled.Count > 0)
        {
            onOutput?.Invoke("Stopping disabled services: " + string.Join(", ", disabled.Select(container => container.Service).Distinct(StringComparer.Ordinal)) +
                             ". Their data volumes are preserved.");
            var ids = disabled.Select(container => container.Id).ToArray();

            if (!RunCommand(target, ["stop", "--time", "30", "--", .. ids], TimeSpan.FromMinutes(1), _cancellationToken, onOutput, captureOutput: false).Succeeded)
            {
                return false;
            }

            if (!RunCommand(target, ["rm", "--", .. ids], QueryTimeout, _cancellationToken, onOutput, captureOutput: false).Succeeded)
            {
                return false;
            }
        }

        return RunCompose(target, ["up", "-d"], onOutput).Succeeded;
    }

    public bool WaitForMonitoring(string target, TimeSpan timeout, Action<string>? onOutput = null)
    {
        _cancellationToken.ThrowIfCancellationRequested();
        var endpoints = new[]
        {
            "http://prometheus:9090/-/ready", "http://loki:3100/ready",
            "http://alloy:12345/-/ready", "http://grafana:3000/monitoring/api/health",
        };
        var pending = new HashSet<string>(endpoints, StringComparer.Ordinal);
        var started = _timeProvider.GetTimestamp();
        var latest = new Dictionary<string, string>(StringComparer.Ordinal);

        while (Remaining(started, timeout) > TimeSpan.Zero)
        {
            _cancellationToken.ThrowIfCancellationRequested();

            foreach (var endpoint in pending.ToArray())
            {
                var available = Remaining(started, timeout);

                if (available <= TimeSpan.Zero) break;

                var result = RunCompose(target, ["exec", "-T", "grafana", "wget", "-qO-", endpoint],
                    timeout: Min(available, TimeSpan.FromSeconds(5)), captureOutput: true);
                latest[endpoint] = DescribeResult(result);

                if (result.Succeeded && Remaining(started, timeout) > TimeSpan.Zero)
                {
                    pending.Remove(endpoint);
                    onOutput?.Invoke("Ready: " + endpoint);
                }
            }

            if (pending.Count == 0) return true;

            DelayBeforePoll(started, timeout);
        }

        var diagnostic = $"Monitoring readiness is incomplete after {Elapsed(started).TotalSeconds:0.0}s: " +
                         string.Join(", ", pending.Select(endpoint => endpoint + " (" + latest.GetValueOrDefault(endpoint, "not probed") + ")"));
        onOutput?.Invoke(CreateRedactor(ResolveTarget(target)).Redact(diagnostic));

        return false;
    }

    public bool IsHttpsPortOwned(string target, int port)
    {
        if (_projectName is null) throw new InvalidOperationException("Configure the installation identity before checking its HTTPS port.");

        var containers = RunQuery(target,
            ["ps", "--quiet", "--filter", "label=com.docker.compose.project=" + _projectName, "--filter", "label=com.docker.compose.service=nginx"],
            _cancellationToken, null);
        var ids = Lines(containers.StandardOutput).ToArray();

        if (ids.Length == 0) return false;

        var inspected = RunQuery(target, new[] { "inspect", "--format", "{{json .NetworkSettings.Ports}}" }.Concat(ids).ToArray(),
            _cancellationToken, null);

        foreach (var ports in DockerJson.Objects(inspected.StandardOutput))
        {
            if (!ports.TryGetProperty("443/tcp", out var bindings) || bindings.ValueKind != JsonValueKind.Array) continue;

            if (bindings.EnumerateArray().Any(binding => DockerJson.Text(binding, "HostPort") == port.ToString(System.Globalization.CultureInfo.InvariantCulture))) return true;
        }

        return false;
    }

    public bool ComposeDown(string target) => RunCompose(target, ["down"]).Succeeded;

    public bool ValidateConfiguration(string target, Action<string>? onOutput = null) =>
        RunCompose(target, ["config", "--quiet"], onOutput).Succeeded;

    public IReadOnlyList<string> GetExpectedServices(string target, Action<string>? onOutput = null)
        => ReadExpectedServices(target, QueryTimeout, onOutput);

    private IReadOnlyList<string> ReadExpectedServices(string target, TimeSpan timeout, Action<string>? onOutput)
    {
        var result = RunCompose(target, ["config", "--services"], onOutput, timeout, captureOutput: true);

        if (!result.Succeeded)
        {
            throw new InvalidOperationException("The expected Compose service inventory could not be read.");
        }

        var services = Lines(result.StandardOutput).Distinct(StringComparer.Ordinal).ToList();

        if (services.Count == 0 || services.Any(service => !Regex.IsMatch(service, "^[a-zA-Z0-9][a-zA-Z0-9_.-]*$")))
        {
            throw new InvalidOperationException("Compose returned an empty or invalid service inventory.");
        }

        return services;
    }

    public IReadOnlyList<ContainerStatus> GetServiceStatuses(string target, Action<string>? onOutput = null)
        => ReadServiceStatuses(target, QueryTimeout, onOutput);

    public IReadOnlyList<string> GetServiceLogs(string target, string service, int tail = 100)
    {
        if (tail is < 1 or > 1000) throw new ArgumentOutOfRangeException(nameof(tail));

        if (!GetExpectedServices(target).Contains(service, StringComparer.Ordinal))
        {
            throw new ArgumentException("Choose an exact service name from this installation's Compose inventory.", nameof(service));
        }

        var result = RunCompose(target, ["logs", "--no-color", "--tail", tail.ToString(System.Globalization.CultureInfo.InvariantCulture), service],
            captureOutput: true);
        var redactor = CreateRedactor(ResolveTarget(target));

        if (!result.Succeeded)
        {
            throw new InvalidOperationException("Service logs could not be read: " + redactor.Redact(DescribeResult(result)));
        }

        return Lines(result.StandardOutput).Concat(Lines(result.StandardError)).Select(redactor.Redact).ToList();
    }

    private IReadOnlyList<ContainerStatus> ReadServiceStatuses(string target, TimeSpan timeout, Action<string>? onOutput)
    {
        var result = RunCompose(target, ["ps", "--all", "--format", "json"], onOutput, timeout, captureOutput: true);

        if (!result.Succeeded)
        {
            throw new InvalidOperationException("Docker could not inspect the installation's containers: " +
                                                CreateRedactor(ResolveTarget(target)).Redact(DescribeResult(result)));
        }

        return ParseStatuses(result.StandardOutput);
    }

    public List<string> GetContainerStatuses(string target, Action<string>? onOutput = null) =>
        GetServiceStatuses(target, onOutput).Select(status => status.Display).ToList();

    public bool WaitForHealthy(string target, TimeSpan timeout, IReadOnlyCollection<string> oneShotContainers,
        Action<IReadOnlyList<string>>? onPoll = null)
    {
        _cancellationToken.ThrowIfCancellationRequested();
        if (timeout <= TimeSpan.Zero) return false;

        var started = _timeProvider.GetTimestamp();
        var expected = ReadExpectedServices(target, Min(timeout, QueryTimeout), null);
        var remaining = Remaining(started, timeout);

        return remaining > TimeSpan.Zero && WaitForServices(target, expected, remaining, oneShotContainers,
            statuses => onPoll?.Invoke(statuses.Select(status => status.Display).ToList()));
    }

    public bool WaitForServices(string target, IReadOnlyCollection<string> expectedServices, TimeSpan timeout,
        IReadOnlyCollection<string> oneShotServices, Action<IReadOnlyList<ContainerStatus>>? onPoll = null,
        Action<string>? onOutput = null)
    {
        _cancellationToken.ThrowIfCancellationRequested();
        var started = _timeProvider.GetTimestamp();
        IReadOnlyList<ContainerStatus> latest = [];

        if (expectedServices.Count == 0)
        {
            onOutput?.Invoke("Container readiness failed: the expected Compose service inventory is empty.");
            return false;
        }

        while (Remaining(started, timeout) > TimeSpan.Zero)
        {
            _cancellationToken.ThrowIfCancellationRequested();
            var available = Remaining(started, timeout);

            if (available <= TimeSpan.Zero)
            {
                break;
            }

            IReadOnlyList<ContainerStatus> statuses;

            try
            {
                statuses = ReadServiceStatuses(target, Min(available, QueryTimeout), onOutput);
            }
            catch (Exception exception) when (exception is InvalidOperationException or JsonException)
            {
                onOutput?.Invoke($"Container readiness failed after {Elapsed(started).TotalSeconds:0.0}s: {exception.Message}");
                return false;
            }

            latest = statuses;
            onPoll?.Invoke(statuses);
            var readiness = ContainerReadiness.Evaluate(expectedServices, statuses, oneShotServices);

            if (readiness.Ready && Remaining(started, timeout) > TimeSpan.Zero)
            {
                return true;
            }

            foreach (var problem in readiness.Problems)
            {
                onOutput?.Invoke($"After {Elapsed(started).TotalSeconds:0.0}s: {problem}");
            }

            if (readiness.Failed)
            {
                return false;
            }

            DelayBeforePoll(started, timeout);
        }

        var blocking = ContainerReadiness.Evaluate(expectedServices, latest, oneShotServices).Problems;
        onOutput?.Invoke($"Container readiness timed out after {Elapsed(started).TotalSeconds:0.0}s: " +
                         string.Join("; ", blocking.Count > 0 ? blocking : ["the final probe exceeded its deadline"]));
        return false;
    }

    internal static IReadOnlyList<ContainerStatus> ParseStatuses(string output)
    {
        var statuses = new List<ContainerStatus>();

        foreach (var element in DockerJson.Objects(output))
        {
            var service = DockerJson.Text(element, "Service");
            var state = DockerJson.Text(element, "State").ToLowerInvariant();

            if (string.IsNullOrWhiteSpace(service) || string.IsNullOrWhiteSpace(state))
            {
                throw new InvalidOperationException("Docker returned a container without a service identifier or state.");
            }

            int? exitCode = null;

            if (element.TryGetProperty("ExitCode", out var code) && code.ValueKind != JsonValueKind.Null)
            {
                if (code.ValueKind == JsonValueKind.Number && code.TryGetInt32(out var number))
                {
                    exitCode = number;
                }
                else if (code.ValueKind == JsonValueKind.String && int.TryParse(code.GetString(), out number))
                {
                    exitCode = number;
                }
                else
                {
                    throw new InvalidOperationException("Docker returned an invalid container exit code.");
                }
            }

            statuses.Add(new ContainerStatus(service, DockerJson.Text(element, "Name"), state,
                DockerJson.Text(element, "Health").ToLowerInvariant(), exitCode));
        }

        return statuses;
    }

    public LegacyInstallationIdentity DiscoverLegacyIdentity(string target, Action<string>? onOutput = null,
        CancellationToken cancellationToken = default)
    {
        target = ResolveTarget(target);
        return new DockerOwnershipInspector(arguments => RunQuery(target, arguments, cancellationToken, onOutput)).Discover(target);
    }

    public void VerifyInstallationOwnership(string target, InstallationConfig config, Action<string>? onOutput = null)
    {
        target = ResolveTarget(target);
        new DockerOwnershipInspector(arguments => RunQuery(target, arguments, _cancellationToken, onOutput)).Verify(target, config);
    }

    private static IEnumerable<string> Lines(string output) =>
        output.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    private TimeSpan Elapsed(long started) => _timeProvider.GetElapsedTime(started);

    private TimeSpan Remaining(long started, TimeSpan timeout) => timeout - Elapsed(started);

    private static TimeSpan Min(TimeSpan first, TimeSpan second) => first < second ? first : second;

    private void DelayBeforePoll(long started, TimeSpan timeout)
    {
        var remaining = Remaining(started, timeout);

        if (remaining > TimeSpan.Zero && PollInterval > TimeSpan.Zero)
        {
            Task.Delay(Min(remaining, PollInterval), _timeProvider, _cancellationToken).GetAwaiter().GetResult();
        }
    }

    private static string DescribeResult(ProcessResult result)
    {
        var state = result.TimedOut ? "command timed out" : $"exit {result.ExitCode}";
        var diagnostic = result.StandardError.Trim();
        return diagnostic.Length == 0 ? state : state + ": " + diagnostic;
    }

    private SecretRedactor CreateRedactor(string target)
    {
        var values = new Dictionary<string, string>(StringComparer.Ordinal);

        if (_config is not null)
        {
            foreach (var pair in _config.Credentials.Concat(_config.EnvironmentValues))
            {
                values[pair.Key] = pair.Value;
            }
        }

        var path = Path.Combine(target, ".env");

        if (File.Exists(path))
        {
            foreach (var pair in EnvGenerator.Parse(File.ReadAllText(path)))
            {
                values[pair.Key] = pair.Value;
            }
        }

        return new SecretRedactor(() => values);
    }

    private string ResolveTarget(string input)
    {
        if (!PathResolver.TryResolve(input, _launchDirectory, out var target, out var error))
        {
            throw new ArgumentException(error, nameof(input));
        }

        if (PathResolver.ValidateTarget(target, probeWritable: false) is { } targetError)
        {
            throw new ArgumentException(targetError, nameof(input));
        }

        return target;
    }

    private ProcessResult RunCompose(string target, IReadOnlyList<string> arguments, Action<string>? onOutput = null,
        TimeSpan? timeout = null, bool captureOutput = false)
    {
        if (_projectName is null)
        {
            throw new InvalidOperationException("Configure the persisted installation project identity before running Docker Compose.");
        }

        target = ResolveTarget(target);
        var command = new[]
        {
            "compose", "--project-name", _projectName, "--project-directory", target,
            "-f", Path.Combine(target, "docker-compose.yml"), "--env-file", Path.Combine(target, ".env"),
        }.Concat(arguments).ToArray();

        return RunCommand(target, command, timeout ?? QueryTimeout, _cancellationToken, onOutput, captureOutput);
    }

    private ProcessResult RunQuery(string target, IReadOnlyList<string> arguments, CancellationToken token, Action<string>? onOutput)
    {
        // Ownership must be checked before a fresh target directory is created.
        var result = RunCommand(_launchDirectory, arguments, QueryTimeout, token, onOutput, captureOutput: true);

        if (!result.Succeeded)
        {
            throw new InvalidOperationException("Docker ownership inspection failed. Check daemon access before changing installation files.");
        }

        return result;
    }

    private ProcessResult RunCommand(string target, IReadOnlyList<string> arguments, TimeSpan timeout,
        CancellationToken token, Action<string>? onOutput, bool captureOutput)
    {
        token.ThrowIfCancellationRequested();
        var info = new ProcessStartInfo
        {
            FileName = "docker",
            WorkingDirectory = target,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        foreach (var argument in arguments)
        {
            info.ArgumentList.Add(argument);
        }

        if (arguments.Count > 0 && arguments[0] == "compose")
        {
            var environmentPath = Path.Combine(target, ".env");

            if (File.Exists(environmentPath))
            {
                foreach (var key in EnvGenerator.Parse(File.ReadAllText(environmentPath)).Keys)
                {
                    info.Environment.Remove(key);
                }
            }

            foreach (var key in new[] { "COMPOSE_FILE", "COMPOSE_ENV_FILES", "COMPOSE_PROJECT_NAME", "COMPOSE_PROFILES" })
            {
                info.Environment.Remove(key);
            }
        }

        var redactor = CreateRedactor(target);
        Action<string>? report = onOutput is null ? null : line => onOutput(redactor.Redact(line));
        report?.Invoke($"$ docker {string.Join(" ", arguments.Select(QuoteForDisplay))}");

        var metadata = new ProcessStartInfo { FileName = info.FileName, WorkingDirectory = info.WorkingDirectory };
        metadata.Environment.Clear();

        foreach (var argument in arguments)
        {
            metadata.ArgumentList.Add(redactor.Redact(argument));
        }

        _onCommand?.Invoke(metadata, null);
        ProcessResult? completed = null;

        try
        {
            var result = _runner.Run(info, timeout, token,
                captureOutput ? null : report, report, captureOutput);
            completed = new ProcessResult(result.ExitCode, string.Empty, string.Empty, result.TimedOut);

            if (result.TimedOut)
            {
                report?.Invoke($"[error] Docker command timed out after {timeout.TotalSeconds:0.#} seconds.");
            }
            else if (result.ExitCode != 0)
            {
                report?.Invoke($"[exit {result.ExitCode}]");
            }

            return result;
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            completed = new ProcessResult(-1, string.Empty, string.Empty);
            report?.Invoke($"[error] {exception.Message}");
            return new ProcessResult(-1, string.Empty, exception.Message);
        }
        finally
        {
            _onCommand?.Invoke(metadata, completed ?? new ProcessResult(-1, string.Empty, string.Empty));
        }
    }

    private static string QuoteForDisplay(string argument) =>
        argument.All(character => char.IsLetterOrDigit(character) || "-_/.:".Contains(character))
            ? argument
            : "'" + argument.Replace("'", "'\"'\"'") + "'";

    internal static void PumpProcessOutput(Process process, Action<string>? onOutput) =>
        ProcessRunner.Drain(process, TimeSpan.FromMinutes(5), CancellationToken.None, onOutput, onOutput, captureOutput: false);
}
