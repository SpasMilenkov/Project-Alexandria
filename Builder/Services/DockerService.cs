using System.Diagnostics;

namespace Builder.Services;

public interface IDockerService
{
    bool ComposeBuild(string workingDirectory, Action<string>? onOutput = null);
    bool ComposeUp(string workingDirectory, Action<string>? onOutput = null);
    bool ComposeDown(string workingDirectory);
    bool WaitForHealthy(string workingDirectory, TimeSpan timeout);
    List<string> GetContainerStatuses(string workingDirectory);
}

public class DockerService : IDockerService
{
    public bool ComposeBuild(string workingDirectory, Action<string>? onOutput = null)
    {
        return RunDockerCompose(workingDirectory, "build", onOutput);
    }

    public bool ComposeUp(string workingDirectory, Action<string>? onOutput = null)
    {
        return RunDockerCompose(workingDirectory, "up -d", onOutput);
    }

    public bool ComposeDown(string workingDirectory)
    {
        return RunDockerCompose(workingDirectory, "down");
    }

    public bool WaitForHealthy(string workingDirectory, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;

        while (DateTime.UtcNow < deadline)
        {
            var statuses = GetContainerStatuses(workingDirectory);

            if (statuses.Count > 0 && statuses.All(s =>
                    s.Contains("healthy", StringComparison.OrdinalIgnoreCase) ||
                    (s.Contains("running", StringComparison.OrdinalIgnoreCase) &&
                     !s.Contains("health", StringComparison.OrdinalIgnoreCase))))
            {
                return true;
            }

            // Check for any exited/dead containers (except init containers)
            if (statuses.Any(s =>
                    s.Contains("exited", StringComparison.OrdinalIgnoreCase) &&
                    !s.Contains("init", StringComparison.OrdinalIgnoreCase)))
            {
                return false;
            }

            Thread.Sleep(5000);
        }

        return false;
    }

    public List<string> GetContainerStatuses(string workingDirectory)
    {
        var statuses = new List<string>();

        var startInfo = new ProcessStartInfo
        {
            FileName = "docker",
            Arguments = "compose ps --format \"{{.Name}}: {{.Status}}\"",
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        try
        {
            using var process = Process.Start(startInfo);
            if (process == null) return statuses;

            while (!process.StandardOutput.EndOfStream)
            {
                var line = process.StandardOutput.ReadLine();
                if (!string.IsNullOrWhiteSpace(line))
                    statuses.Add(line);
            }

            process.WaitForExit();
        }
        catch
        {
            // Ignore errors when polling
        }

        return statuses;
    }

    private static bool RunDockerCompose(string workingDirectory, string arguments, Action<string>? onOutput = null)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = "docker",
            Arguments = $"compose {arguments}",
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        try
        {
            using var process = Process.Start(startInfo);
            if (process == null) return false;

            // Read output line by line
            while (!process.StandardOutput.EndOfStream)
            {
                var line = process.StandardOutput.ReadLine();
                if (line != null)
                    onOutput?.Invoke(line);
            }

            // Also capture stderr (docker compose often writes progress to stderr)
            while (!process.StandardError.EndOfStream)
            {
                var line = process.StandardError.ReadLine();
                if (line != null)
                    onOutput?.Invoke(line);
            }

            process.WaitForExit();
            return process.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }
}
