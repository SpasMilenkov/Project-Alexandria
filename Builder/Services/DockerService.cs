using System.Diagnostics;
using System.Text.RegularExpressions;

namespace Builder.Services;

public interface IDockerService
{
    bool ComposeBuild(string workingDirectory, Action<string>? onOutput = null);

    bool ComposeUpCore(string workingDirectory, Action<string>? onOutput = null);

    bool ComposeUpAll(string workingDirectory, Action<string>? onOutput = null);

    bool ComposeDown(string workingDirectory);

    bool WaitForHealthy(
        string workingDirectory,
        TimeSpan timeout,
        IReadOnlyCollection<string> oneShotContainers,
        Action<IReadOnlyList<string>>? onPoll = null);

    List<string> GetContainerStatuses(string workingDirectory, Action<string>? onOutput = null);
}

public class DockerService : IDockerService
{
    private static readonly Regex StatusLineName = new(@"(?<name>[\w-]+):\s", RegexOptions.Compiled);

    // Test seam: lets the unit suite substitute a controlled process instead
    // of requiring a real docker binary (InternalsVisibleTo Builder.Tests)
    internal static Func<ProcessStartInfo, Process?> StartProcess = info => Process.Start(info);

    public bool ComposeBuild(string workingDirectory, Action<string>? onOutput = null)
    {
        return RunDockerCompose(workingDirectory, "build", onOutput);
    }

    // Phase 1 of the two-phase boot: infrastructure only, so garage-init can
    // produce the S3 keys before any app container needs them (D6)
    public bool ComposeUpCore(string workingDirectory, Action<string>? onOutput = null)
    {
        return RunDockerCompose(
            workingDirectory,
            "up -d postgres rabbitmq-config rabbitmq garage garage-init",
            onOutput);
    }

    // Phase 2: everything the user selected (compose skips nothing here -
    // core services are already running and remain untouched)
    public bool ComposeUpAll(string workingDirectory, Action<string>? onOutput = null)
    {
        return RunDockerCompose(workingDirectory, "up -d", onOutput);
    }

    public bool ComposeDown(string workingDirectory)
    {
        return RunDockerCompose(workingDirectory, "down");
    }

    public bool WaitForHealthy(
        string workingDirectory,
        TimeSpan timeout,
        IReadOnlyCollection<string> oneShotContainers,
        Action<IReadOnlyList<string>>? onPoll = null)
    {
        var deadline = DateTime.UtcNow + timeout;

        while (DateTime.UtcNow < deadline)
        {
            var statuses = GetContainerStatuses(workingDirectory);

            onPoll?.Invoke(statuses);

            if (statuses.Count == 0)
            {
                Thread.Sleep(5000);
                continue;
            }

            foreach (var status in statuses)
            {
                if (!IsContainerSettled(status, oneShotContainers))
                {
                    goto StillWaiting;
                }
            }

            return true;

            StillWaiting: ;
            Thread.Sleep(5000);
        }

        return false;
    }

    // A line counts as settled when the container is healthy, plainly
    // running, or exited cleanly as an expected one-shot job. Any other exit
    // means the stack is broken and waiting more will not help.
    private static bool IsContainerSettled(string statusLine, IReadOnlyCollection<string> oneShotContainers)
    {
        var nameMatch = StatusLineName.Match(statusLine);
        var name = nameMatch.Success ? nameMatch.Groups["name"].Value : string.Empty;
        var isOneShot = oneShotContainers.Any(name.Contains);

        if (statusLine.Contains("Exited (0)", StringComparison.OrdinalIgnoreCase) && isOneShot)
        {
            return true;
        }

        if (statusLine.Contains("healthy", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var isRunning = statusLine.Contains("Up ", StringComparison.OrdinalIgnoreCase) ||
                        statusLine.Contains("running", StringComparison.OrdinalIgnoreCase);

        return isRunning && !statusLine.Contains("health: starting", StringComparison.OrdinalIgnoreCase);
    }

    public List<string> GetContainerStatuses(string workingDirectory, Action<string>? onOutput = null)
    {
        var statuses = new List<string>();

        var startInfo = new ProcessStartInfo
        {
            FileName = "docker",
            Arguments = $"{BuildBaseArguments(workingDirectory)} ps --format \"{{{{.Names}}}}: {{{{.Status}}}}\"",
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        try
        {
            using var process = StartProcess(startInfo);

            if (process == null)
            {
                onOutput?.Invoke("[error] docker process could not be started");

                return statuses;
            }

            while (process.StandardOutput.ReadLine() is { } line)
            {
                if (!string.IsNullOrWhiteSpace(line))
                    statuses.Add(line);
            }

            while (process.StandardError.ReadLine() is { } line)
            {
                onOutput?.Invoke(line);
            }

            process.WaitForExit();

            if (process.ExitCode != 0)
            {
                onOutput?.Invoke($"[exit {process.ExitCode}]");
            }
        }
        catch (Exception ex)
        {
            onOutput?.Invoke($"[error] {ex.Message}");
        }

        return statuses;
    }

    // Generated compose files live outside the source tree whenever the user
    // picked a custom target, so every invocation pins file + project dir +
    // env file explicitly instead of relying on the working directory (D13)
    private static string BuildBaseArguments(string workingDirectory)
    {
        var composeFile = Path.Combine(workingDirectory, "docker-compose.yml");
        var envFile = Path.Combine(workingDirectory, ".env");

        return $"compose --project-directory \"{workingDirectory}\" " +
               $"-f \"{composeFile}\" --env-file \"{envFile}\"";
    }

    private static bool RunDockerCompose(string workingDirectory, string arguments, Action<string>? onOutput = null)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = "docker",
            Arguments = $"{BuildBaseArguments(workingDirectory)} {arguments}",
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        // D54: the command itself is the first piece of evidence when a spawn
        // fails before the child ever writes a byte
        onOutput?.Invoke($"$ docker {startInfo.Arguments}");

        try
        {
            using var process = StartProcess(startInfo);

            if (process == null)
            {
                onOutput?.Invoke("[error] docker process could not be started");

                return false;
            }

            PumpProcessOutput(process, onOutput);

            process.WaitForExit();

            var exitCode = process.ExitCode;

            if (exitCode != 0)
            {
                onOutput?.Invoke($"[exit {exitCode}]");

                return false;
            }

            return true;
        }
        catch (Exception ex)
        {
            onOutput?.Invoke($"[error] {ex.Message}");

            return false;
        }
    }

    // D54: both pipes are drained concurrently. The previous sequential loop
    // (stdout to EOF, then stderr) deadlocked once stderr filled its pipe
    // buffer while stdout sat idle, and reordered interleaved output.
    internal static void PumpProcessOutput(Process process, Action<string>? onOutput)
    {
        if (onOutput is null)
        {
            DrainSilently(process);

            return;
        }

        // onOutput touches UI state - serialize the two pumps
        var gate = new object();

        var stdout = Task.Run(() =>
        {
            while (process.StandardOutput.ReadLine() is { } line)
            {
                lock (gate)
                {
                    onOutput(line);
                }
            }
        });

        while (process.StandardError.ReadLine() is { } line)
        {
            lock (gate)
            {
                onOutput(line);
            }
        }

        stdout.Wait();
    }

    private static void DrainSilently(Process process)
    {
        var stdout = Task.Run(() => process.StandardOutput.ReadToEnd());

        process.StandardError.ReadToEnd();
        stdout.Wait();
    }
}
