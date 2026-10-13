using System.Diagnostics;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;
using System.Globalization;
using System.Text.Json;
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

    bool IsPortAvailable(int port);

    string GetProcessUsingPort(int port);

    (string command, string url) GetDockerInstallInstructions();

    string GetDockerFailureDetails() => string.Empty;

    string? GetDockerContextError() => null;

    string? GetHostPlatformError() => null;

    string? GetDockerPlatformError() => null;

    void ConfigureCancellation(CancellationToken cancellationToken) { }
}

public class SystemChecker : ISystemChecker
{
    private readonly IProcessRunner _runner;
    private CancellationToken _cancellationToken;
    internal Func<string, string?> GetEnvironmentVariable { get; init; } = Environment.GetEnvironmentVariable;
    internal TimeSpan ProbeTimeout { get; init; } = TimeSpan.FromSeconds(10);
    internal Func<OSPlatform> DetectOperatingSystem { get; init; } = GetRuntimeOperatingSystem;
    internal Func<Architecture> DetectArchitecture { get; init; } = () => RuntimeInformation.OSArchitecture;
    internal Func<bool> DetectGlibc { get; init; } = HasGlibc;

    private string _lastFailure = string.Empty;

    public SystemChecker() : this(new ProcessRunner())
    {
    }

    public SystemChecker(IProcessRunner runner, CancellationToken cancellationToken = default)
    {
        _runner = runner;
        _cancellationToken = cancellationToken;
    }

    public void ConfigureCancellation(CancellationToken cancellationToken) => _cancellationToken = cancellationToken;

    public OSPlatform GetOperatingSystem() => DetectOperatingSystem();

    private static OSPlatform GetRuntimeOperatingSystem()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            return OSPlatform.Linux;
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return OSPlatform.Windows;
        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            return OSPlatform.OSX;

        return OSPlatform.Create("Unknown");
    }

    public string? GetHostPlatformError()
    {
        var os = GetOperatingSystem();
        var architecture = DetectArchitecture();

        if (os != OSPlatform.Linux || architecture != Architecture.X64)
        {
            return "This installer supports glibc-based Linux x64 hosts only. " +
                   $"Detected {os} {architecture.ToString().ToLowerInvariant()}. " +
                   "Run setup on a supported Linux x64 machine.";
        }

        if (!DetectGlibc())
        {
            return "This installer requires glibc on its Linux x64 host. " +
                   "Musl-based hosts, including Alpine Linux, are unsupported. Use a glibc-based Linux distribution.";
        }

        return null;
    }

    public string? GetDockerPlatformError()
    {
        const string format = "{\"OSType\":{{json .OSType}},\"Architecture\":{{json .Architecture}}}";

        if (!ExecuteCommand("docker", ["info", "--format", format], out var output))
        {
            return "The Docker daemon platform could not be verified. " + _lastFailure;
        }

        try
        {
            using var document = JsonDocument.Parse(output);
            var os = DockerJson.Text(document.RootElement, "OSType").Trim();
            var architecture = DockerJson.Text(document.RootElement, "Architecture").Trim();
            var supportedArchitecture = architecture.Equals("amd64", StringComparison.OrdinalIgnoreCase) ||
                                        architecture.Equals("x86_64", StringComparison.OrdinalIgnoreCase);

            if (os.Equals("linux", StringComparison.OrdinalIgnoreCase) && supportedArchitecture)
            {
                return null;
            }

            if (string.IsNullOrWhiteSpace(os) || string.IsNullOrWhiteSpace(architecture))
            {
                return "Docker did not report its daemon operating system and architecture. " +
                       "A local Linux amd64 Docker daemon is required; check docker info before retrying.";
            }

            return $"This installer requires a local Linux amd64 Docker daemon. Detected {os} {architecture}. " +
                   "Windows containers, ARM daemons, and cross-platform emulation are unsupported.";
        }
        catch (Exception error) when (error is JsonException or InvalidOperationException)
        {
            return "Docker returned unrecognized daemon platform information. " +
                   "A local Linux amd64 Docker daemon is required; check docker info before retrying.";
        }
    }

    private static bool HasGlibc()
    {
        if (!NativeLibrary.TryLoad("libc.so.6", out var library))
        {
            return false;
        }

        try
        {
            return NativeLibrary.TryGetExport(library, "gnu_get_libc_version", out _);
        }
        finally
        {
            NativeLibrary.Free(library);
        }
    }

    public bool IsDockerInstalled()
    {
        return ExecuteCommand("docker", ["--version"], out _);
    }

    public bool IsDockerComposeInstalled()
    {
        return ExecuteCommand("docker", ["compose", "version"], out _);
    }

    public bool IsDockerDaemonRunning()
    {
        return ExecuteCommand("docker", ["info", "--format", "{{.ServerVersion}}"], out _);
    }

    public bool IsGitInstalled()
    {
        return ExecuteCommand("git", ["--version"], out _);
    }

    public string GetDockerVersion()
    {
        ExecuteCommand("docker", ["--version"], out var output);
        return output.Trim();
    }

    public string GetDockerComposeVersion()
    {
        ExecuteCommand("docker", ["compose", "version"], out var output);
        return output.Trim();
    }

    public string GetGitVersion()
    {
        ExecuteCommand("git", ["--version"], out var output);
        return output.Trim();
    }

    public string GetDockerFailureDetails() => _lastFailure;

    public string? GetDockerContextError()
    {
        var context = GetEnvironmentVariable("DOCKER_CONTEXT");
        var endpoint = GetEnvironmentVariable("DOCKER_HOST");

        // An explicit context takes precedence over DOCKER_HOST in the Docker CLI.
        if (!string.IsNullOrWhiteSpace(context) || string.IsNullOrWhiteSpace(endpoint))
        {
            if (string.IsNullOrWhiteSpace(context))
            {
                if (!ExecuteCommand("docker", ["context", "show"], out var selectedContext))
                {
                    return $"The selected Docker context could not be read. Check docker context ls. {_lastFailure}";
                }

                context = selectedContext.Trim();
            }

            if (string.IsNullOrWhiteSpace(context) ||
                !ExecuteCommand("docker", ["context", "inspect", "--format", "{{.Endpoints.docker.Host}}", context], out endpoint))
            {
                return $"The selected Docker context is unavailable. Check DOCKER_CONTEXT and docker context ls. {_lastFailure}";
            }
        }

        endpoint = endpoint?.Trim();

        if (endpoint is not null &&
            (endpoint.StartsWith("unix://", StringComparison.OrdinalIgnoreCase) ||
             endpoint.StartsWith("npipe://", StringComparison.OrdinalIgnoreCase)))
        {
            return null;
        }

        return "This installer needs Docker on this machine so it can mount the local source and settings folders. " +
               "SSH, TCP, and unrecognized Docker connections are not supported. Select a local Docker context " +
               "and check DOCKER_HOST and DOCKER_CONTEXT before retrying.";
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
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            resources.TotalMemoryMb = 0;
            resources.AvailableMemoryMb = 0;
        }

        var contextError = GetDockerContextError();

        if (contextError is not null)
        {
            resources.DiskSpaceDetails = contextError;
            return resources;
        }

        const string format = "{\"DockerRootDir\":{{json .DockerRootDir}},\"OperatingSystem\":{{json .OperatingSystem}},\"NCPU\":{{.NCPU}},\"MemTotal\":{{.MemTotal}}}";

        if (ExecuteCommand("docker", ["info", "--format", format], out var dockerInfo))
        {
            PopulateDockerResources(resources, dockerInfo, RuntimeInformation.IsOSPlatform(OSPlatform.Linux), MeasureFreeBytes);
        }
        else
        {
            resources.DiskSpaceDetails = "The Docker daemon's data filesystem could not be inspected. " + _lastFailure;
        }

        return resources;
    }

    internal static void PopulateDockerResources(
        SystemResources resources, string dockerInfo, bool linuxHost, Func<string, long?> measureFreeBytes)
    {
        resources.AvailableDiskMb = 0;
        resources.DockerAvailableDiskMb = null;

        try
        {
            using var document = JsonDocument.Parse(dockerInfo);
            var root = document.RootElement;
            var operatingSystem = DockerJson.Text(root, "OperatingSystem");
            resources.DockerDataDirectory = DockerJson.Text(root, "DockerRootDir");

            if (root.TryGetProperty("NCPU", out var cpu) && cpu.TryGetInt32(out var cores) && cores > 0)
            {
                resources.DockerCpuCores = cores;
                resources.CpuCores = cores;
            }

            if (root.TryGetProperty("MemTotal", out var memory) && memory.TryGetInt64(out var bytes) && bytes > 0)
            {
                resources.DockerMemoryMb = bytes / (1024 * 1024);
                resources.TotalMemoryMb = resources.DockerMemoryMb.Value;
                resources.AvailableMemoryMb = 0;
            }

            if (!linuxHost || operatingSystem.Contains("Docker Desktop", StringComparison.OrdinalIgnoreCase))
            {
                resources.DiskSpaceDetails = "Docker stores its data inside a VM. Check the VM's available disk space in Docker Desktop; host free space is not the Docker data filesystem.";
                return;
            }

            if (!Path.IsPathFullyQualified(resources.DockerDataDirectory))
            {
                resources.DiskSpaceDetails = "Docker did not report a usable local data directory.";
                return;
            }

            var available = measureFreeBytes(resources.DockerDataDirectory);

            if (available.HasValue && available.Value >= 0)
            {
                resources.DockerAvailableDiskMb = available.Value / (1024 * 1024);
                resources.AvailableDiskMb = resources.DockerAvailableDiskMb.Value;
                resources.DiskSpaceDetails = "Measured on the filesystem containing Docker's reported data directory; shared by volumes, images and temporary container data.";
            }
            else
            {
                resources.DiskSpaceDetails = "The local Docker data filesystem is inaccessible. Check available space at " + resources.DockerDataDirectory + "; the installation folder is not a substitute.";
            }
        }
        catch (Exception error) when (error is JsonException or FormatException or InvalidOperationException or OverflowException)
        {
            resources.DiskSpaceDetails = "Docker returned unsupported resource information; its data filesystem free space is unknown.";
        }
    }

    private long? MeasureFreeBytes(string directory)
    {
        if (!ExecuteCommand("stat", ["--file-system", "--format=%a:%S", "--", directory], out var output)) return null;
        var parts = output.Trim().Split(':');

        if (parts.Length != 2 || !long.TryParse(parts[0], NumberStyles.None, CultureInfo.InvariantCulture, out var blocks)
            || !long.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out var size) || blocks < 0 || size <= 0)
        {
            return null;
        }

        try
        {
            return checked(blocks * size);
        }
        catch (OverflowException)
        {
            return null;
        }
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
                ExecuteCommand("netstat", ["-ano"], out var output);
                return string.Join(Environment.NewLine, output.Split('\n')
                    .Where(line => line.Contains($":{port} ", StringComparison.Ordinal)));
            }

            if (os == OSPlatform.Linux || os == OSPlatform.OSX)
            {
                ExecuteCommand("lsof", ["-i", $":{port}"], out var output);
                return output.Trim();
            }

            return "Unknown";
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            return "Unable to determine";
        }
    }

    private bool ExecuteCommand(string command, IReadOnlyList<string> arguments, out string output)
    {
        output = string.Empty;
        _lastFailure = string.Empty;

        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = command,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };

            foreach (var argument in arguments)
            {
                startInfo.ArgumentList.Add(argument);
            }

            var result = _runner.Run(startInfo, ProbeTimeout, _cancellationToken);

            if (result.TimedOut)
            {
                _lastFailure = $"The {command} check timed out after {ProbeTimeout.TotalSeconds:0.#} seconds. " +
                               "Check the selected Docker connection and try again.";
                return false;
            }

            output = result.StandardOutput;

            if (!result.Succeeded)
            {
                var error = result.StandardError.Trim();
                _lastFailure = ExplainFailure(command, error.Length > 0 ? error : output.Trim());
                return false;
            }

            return true;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _lastFailure = $"The {command} command could not run: {ex.Message}";
            return false;
        }
    }

    private static string ExplainFailure(string command, string error)
    {
        if (error.Contains("permission denied", StringComparison.OrdinalIgnoreCase) ||
            error.Contains("access is denied", StringComparison.OrdinalIgnoreCase))
        {
            return "Docker denied access to its connection. Use an account with Docker access and retry; " +
                   "starting the daemon alone will not fix a permission problem.";
        }

        if (error.Contains("context", StringComparison.OrdinalIgnoreCase))
        {
            return "Docker could not use the selected context. Check docker context ls and " +
                   "the DOCKER_HOST and DOCKER_CONTEXT environment settings.";
        }

        if (error.Contains("cannot connect", StringComparison.OrdinalIgnoreCase) ||
            error.Contains("connection refused", StringComparison.OrdinalIgnoreCase) ||
            error.Contains("is the docker daemon running", StringComparison.OrdinalIgnoreCase))
        {
            return "The Docker daemon is unavailable. Start Docker on this machine and check the selected context.";
        }

        return string.IsNullOrWhiteSpace(error)
            ? $"The {command} check failed without a diagnostic. Check that the command works in your terminal."
            : error;
    }
}
