#if TERMINAL_SMOKE_HARNESS
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using Builder.Models;
using Builder.Services;
using Builder.UI;
using Builder.UI.Shell;
using Builder.UI.Shell.Screens;
using Builder.Workflow;

namespace Builder.Tests.Terminal;

internal static class TerminalSmokeHarness
{
    public static int Main(string[] arguments)
    {
        if (arguments.Length != 2 || arguments[0] is not ("silent" or "noisy")) return 2;

        var scratch = Path.GetFullPath(arguments[1]);
        var preferences = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);

        if (!Path.GetFullPath(preferences).StartsWith(scratch + Path.DirectorySeparatorChar, StringComparison.Ordinal))
        {
            Console.Error.WriteLine("The terminal smoke test requires a preference directory inside its temporary target.");

            return 2;
        }

        var context = new InstallationContext
        {
            InstallPath = Path.Combine(scratch, "settings"),
            SourceRoot = Environment.CurrentDirectory,
            Features = new FeatureSelection(),
            IsRepair = true,
        };
        context.Config.ProjectName = "builder-terminal-smoke";
        context.Config.PublicEndpoint = new PublicEndpoint { Host = "192.0.2.101", HttpsPort = 18443 };
        context.Config.EnvironmentValues["SYNTHETIC_PASSWORD"] = "terminal-fixture-secret";
        Directory.CreateDirectory(context.InstallPath);
        Theme.Use(Themes.Default);

        ConsoleCancelEventHandler cancel = (_, eventArgs) =>
        {
            eventArgs.Cancel = true;
            context.Cancellation.Cancel();
        };
        Console.CancelKeyPress += cancel;

        try
        {
            using var shell = new Shell(context);
            var docker = new SmokeDockerService(arguments[0], scratch);
            shell.RunAll([("Installing", active => DeployScreen.Run(active, new SmokeConfigurationService(), docker, new SmokeSystemChecker()))]);
            Console.WriteLine("Terminal smoke returned after cancellation.");

            return context.Cancellation.IsCancellationRequested ? 130 : 1;
        }
        finally
        {
            Console.CancelKeyPress -= cancel;
            context.Cancellation.Dispose();
        }
    }

    private sealed class SmokeSystemChecker : ISystemChecker
    {
        public System.Runtime.InteropServices.OSPlatform GetOperatingSystem() => System.Runtime.InteropServices.OSPlatform.Linux;

        public bool IsDockerInstalled() => true;

        public bool IsDockerComposeInstalled() => true;

        public bool IsDockerDaemonRunning() => true;

        public bool IsGitInstalled() => true;

        public string GetDockerVersion() => "synthetic";

        public string GetDockerComposeVersion() => "synthetic";

        public string GetGitVersion() => "synthetic";

        public SystemResources GetSystemResources() => new();

        public bool IsPortAvailable(int port) => true;

        public string GetProcessUsingPort(int port) => string.Empty;

        public (string command, string url) GetDockerInstallInstructions() => (string.Empty, string.Empty);
    }

    private sealed class SmokeDockerService(string mode, string scratch) : IDockerService
    {
        private CancellationToken _cancellationToken;

        public void Configure(InstallationConfig config, CancellationToken cancellationToken = default) =>
            _cancellationToken = cancellationToken;

        public void VerifyInstallationOwnership(string target, InstallationConfig config, Action<string>? onOutput = null)
        {
        }

        public bool ValidateConfiguration(string target, Action<string>? onOutput = null) => true;

        public IReadOnlyList<string> GetExpectedServices(string target, Action<string>? onOutput = null) =>
            ServiceCatalog.Resolve(new FeatureSelection()).ServiceIds;

        public bool ComposeBuild(string target, Action<string>? onOutput = null)
        {
            const string script = """
                set -eu
                printf '%s\n' "$$" > "$1/child-shell.pid"
                sleep 600 &
                work_pid=$!
                printf '%s\n' "$work_pid" > "$1/child-sleep.pid"
                if [ "$2" = noisy ]; then
                  while kill -0 "$work_pid" 2>/dev/null; do
                    printf 'synthetic build stdout\n'
                    printf 'synthetic build stderr\n' >&2
                    sleep 0.05
                  done
                else
                  wait "$work_pid"
                fi
                """;
            var start = new ProcessStartInfo("/bin/sh")
            {
                WorkingDirectory = scratch,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
            };
            foreach (var argument in new[] { "-c", script, "terminal-smoke-child", scratch, mode }) start.ArgumentList.Add(argument);
            var result = new ProcessRunner().Run(start, TimeSpan.FromMinutes(2), _cancellationToken, onOutput, onOutput);

            return result.Succeeded;
        }

        public bool ComposeUpCore(string target, Action<string>? onOutput = null) => throw Unexpected();

        public bool ComposeUpAll(string target, Action<string>? onOutput = null) => throw Unexpected();

        public bool ComposeDown(string target) => throw Unexpected();

        public bool WaitForHealthy(string target, TimeSpan timeout, IReadOnlyCollection<string> oneShotContainers,
            Action<IReadOnlyList<string>>? onPoll = null) => throw Unexpected();

        public List<string> GetContainerStatuses(string target, Action<string>? onOutput = null) => throw Unexpected();

        private static InvalidOperationException Unexpected() =>
            new("The terminal smoke test must cancel its child build before later deployment or cleanup actions.");
    }

    private sealed class SmokeConfigurationService : IConfigurationService
    {
        public void BeginGeneration(InstallationConfig config, string outputPath)
        {
        }

        public void WriteAllConfigFiles(InstallationConfig config, string outputPath)
        {
        }

        public void SaveConfiguration(InstallationConfig config, string path)
        {
        }

        public InstallationConfig? LoadConfiguration(string path) => null;

        public InstallationConfig CreateConfiguration(FeatureSelection features, Dictionary<string, string> credentials,
            Dictionary<string, int> ports) => throw new NotSupportedException();

        public string GenerateDockerCompose(InstallationConfig config) => throw new NotSupportedException();

        public string GenerateGarageToml(InstallationConfig config) => throw new NotSupportedException();

        public string GeneratePrometheusYml(InstallationConfig? config = null) => throw new NotSupportedException();
    }
}
#endif
