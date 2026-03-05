using System.Diagnostics;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;
using Builder.Models;

namespace Builder.Services;

public interface ISystemChecker
{
    OSPlatform GetOperatingSystem();
    bool IsDockerInstalled();
    bool IsDockerComposeInstalled();
    bool IsDockerDaemonRunning();
    bool IsGitInstalled();
    string GetDockerVersion();
    string GetDockerComposeVersion();
    string GetGitVersion();
    SystemResources GetSystemResources();
    List<PortCheckResult> CheckPorts(List<int> ports);
    bool IsPortAvailable(int port);
    string GetProcessUsingPort(int port);
    (string command, string url) GetDockerInstallInstructions();
}

public class SystemChecker : ISystemChecker
{
    public OSPlatform GetOperatingSystem()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            return OSPlatform.Linux;
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return OSPlatform.Windows;
        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            return OSPlatform.OSX;

        throw new PlatformNotSupportedException();
    }

    public bool IsDockerInstalled()
    {
        return ExecuteCommand("docker", "--version", out _);
    }

    public bool IsDockerComposeInstalled()
    {
        return ExecuteCommand("docker", "compose version", out _) ||
               ExecuteCommand("docker-compose", "--version", out _);
    }

    public bool IsDockerDaemonRunning()
    {
        return ExecuteCommand("docker", "info", out _);
    }

    public bool IsGitInstalled()
    {
        return ExecuteCommand("git", "--version", out _);
    }

    public string GetDockerVersion()
    {
        ExecuteCommand("docker", "--version", out var output);
        return output.Trim();
    }

    public string GetDockerComposeVersion()
    {
        if (ExecuteCommand("docker", "compose version", out var output))
            return output.Trim();

        ExecuteCommand("docker-compose", "--version", out output);
        return output.Trim();
    }

    public string GetGitVersion()
    {
        ExecuteCommand("git", "--version", out var output);
        return output.Trim();
    }

    public SystemResources GetSystemResources()
    {
        var resources = new SystemResources
        {
            CpuCores = System.Environment.ProcessorCount,
        };

        // Get total memory
        try
        {
            var gcMemoryInfo = GC.GetGCMemoryInfo();
            resources.TotalMemoryMb = gcMemoryInfo.TotalAvailableMemoryBytes / (1024 * 1024);
            resources.AvailableMemoryMb = resources.TotalMemoryMb; // Approximate
        }
        catch
        {
            resources.TotalMemoryMb = 0;
            resources.AvailableMemoryMb = 0;
        }

        // Get disk space
        try
        {
            var homePath = System.Environment.GetFolderPath(System.Environment.SpecialFolder.UserProfile);
            var driveInfo = new DriveInfo(Path.GetPathRoot(homePath) ?? "/");
            resources.AvailableDiskMb = driveInfo.AvailableFreeSpace / (1024 * 1024);
        }
        catch
        {
            resources.AvailableDiskMb = 0;
        }

        return resources;
    }

    public (string command, string url) GetDockerInstallInstructions()
    {
        var os = GetOperatingSystem();

        if (os == OSPlatform.Linux)
        {
            return ("curl -fsSL https://get.docker.com | sh", "https://docs.docker.com/engine/install/");
        }

        if (os == OSPlatform.OSX)
        {
            return ("brew install --cask docker", "https://docs.docker.com/desktop/install/mac-install/");
        }

        return ("winget install Docker.DockerDesktop", "https://docs.docker.com/desktop/install/windows-install/");
    }

    public List<PortCheckResult> CheckPorts(List<int> ports)
    {
        var results = new List<PortCheckResult>();

        foreach (var port in ports)
        {
            var isAvailable = IsPortAvailable(port);
            var result = new PortCheckResult
            {
                Port = port,
                IsAvailable = isAvailable,
                ProcessBlocking = isAvailable ? string.Empty : GetProcessUsingPort(port)
            };
            results.Add(result);
        }

        return results;
    }

    public bool IsPortAvailable(int port)
    {
        var ipGlobalProperties = IPGlobalProperties.GetIPGlobalProperties();
        var tcpConnInfoArray = ipGlobalProperties.GetActiveTcpListeners();

        return !tcpConnInfoArray.Any(endpoint => endpoint.Port == port);
    }

    public string GetProcessUsingPort(int port)
    {
        try
        {
            var os = GetOperatingSystem();

            if (os == OSPlatform.Windows)
            {
                ExecuteCommand("netstat", $"-ano | findstr :{port}", out var output);
                return output.Trim();
            }

            if (os == OSPlatform.Linux || os == OSPlatform.OSX)
            {
                ExecuteCommand("lsof", $"-i :{port}", out var output);
                return output.Trim();
            }

            return "Unknown";
        }
        catch
        {
            return "Unable to determine";
        }
    }

    private bool ExecuteCommand(string command, string arguments, out string output)
    {
        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = command,
                Arguments = arguments,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(startInfo);

            if (process is null) throw new InvalidOperationException();

            output = process.StandardOutput.ReadToEnd();
            process.WaitForExit();

            return process.ExitCode == 0;
        }
        catch
        {
            output = string.Empty;
            return false;
        }
    }
}
