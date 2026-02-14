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
    string GetDockerVersion();
    string GetDockerComposeVersion();
    List<PortCheckResult> CheckPorts(List<int> ports);
    bool IsPortAvailable(int port);
    string GetProcessUsingPort(int port);
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

    public string GetDockerVersion()
    {
        ExecuteCommand("docker", "--version", out string output);
        return output.Trim();
    }

    public string GetDockerComposeVersion()
    {
        if (ExecuteCommand("docker", "compose version", out string output))
            return output.Trim();
        
        ExecuteCommand("docker-compose", "--version", out output);
        return output.Trim();
    }

    public List<PortCheckResult> CheckPorts(List<int> ports)
    {
        var results = new List<PortCheckResult>();
        
        foreach (var port in ports)
        {
            var result = new PortCheckResult
            {
                Port = port,
                IsAvailable = IsPortAvailable(port),
                ProcessBlocking = (!IsPortAvailable(port) ? GetProcessUsingPort(port) : null) ?? string.Empty
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
                ExecuteCommand("netstat", $"-ano | findstr :{port}", out string output);
                return output.Trim();
            }
            if (os == OSPlatform.Linux || os == OSPlatform.OSX)
            {
                ExecuteCommand("lsof", $"-i :{port}", out string output);
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