using System.ComponentModel;
using System.Diagnostics;
using System.Text;
using System.Text.Json;
using Builder.Services;

namespace Builder.Tests.Services;

public class ComposeEnvParserTests
{
    [Fact]
    [Trait("Category", "Compose")]
    public async Task WriteFile_specialValues_roundTripThroughInstalledComposeParser()
    {
        try
        {
            var version = await RunDockerAsync(["compose", "version"], null, []);
            Assert.SkipWhen(version.ExitCode != 0, "The Docker Compose CLI is unavailable.");
        }
        catch (Win32Exception)
        {
            Assert.Skip("The Docker CLI is unavailable.");
        }

        var directory = Path.Combine(Path.GetTempPath(), "builder-compose-env", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);

        try
        {
            var samples = new[]
            {
                "cash$VARIABLE${VARIABLE}", "hash # equals= spaces", "it's quoted", "double\"quote",
                "ordinary\\path", "trailing\\", "slash\\'quote", "two\\\\'quote",
                "café 文件 😀", "literal\\t", "'", "\\", "\\\\", "",
                "cash$VARIABLE quote\" and trailing\\", "  leading and trailing  ", "literal$$dollars",
            };

            var values = samples.Select((value, index) => (Key: $"BUILDER_SYNTHETIC_{index:00}", Value: value))
                .ToDictionary(pair => pair.Key, pair => pair.Value);
            var envPath = Path.Combine(directory, ".env");
            var composePath = Path.Combine(directory, "compose.yml");
            var compose = new StringBuilder("services:\n  probe:\n    image: busybox\n    environment:\n");

            foreach (var key in values.Keys)
            {
                compose.Append("      ").Append(key).Append(": ${").Append(key).AppendLine("}");
            }

            EnvGenerator.WriteFile(envPath, values);
            File.WriteAllText(composePath, compose.ToString());
            var arguments = new[]
            {
                "compose", "--project-name", "builder-synthetic-env", "--project-directory", directory,
                "--env-file", envPath, "-f", composePath, "config",
            };

            var environment = await RunDockerAsync([.. arguments, "--environment"], directory, values.Keys);
            Assert.True(environment.ExitCode == 0, "Compose rejected the synthetic environment file.");
            var actual = environment.Output.Split('\n').Select(line => line.TrimEnd('\r'))
                .Where(line => line.StartsWith("BUILDER_SYNTHETIC_", StringComparison.Ordinal))
                .Select(line => line.Split('=', 2))
                .ToDictionary(parts => parts[0], parts => parts[1]);

            foreach (var (key, value) in values)
            {
                Assert.True(actual.TryGetValue(key, out var parsed) && parsed == value,
                    $"Compose altered synthetic dotenv case {key}.");
            }

            var config = await RunDockerAsync([.. arguments, "--format", "json"], directory, values.Keys);
            Assert.True(config.ExitCode == 0, "Compose rejected the synthetic service configuration.");
            using var document = JsonDocument.Parse(config.Output);
            var resolved = document.RootElement.GetProperty("services").GetProperty("probe").GetProperty("environment");

            foreach (var (key, value) in values)
            {
                // Compose escapes dollars again when serializing reusable configuration.
                Assert.True(resolved.GetProperty(key).GetString() == value.Replace("$", "$$"),
                    $"Compose altered synthetic service case {key}.");
            }
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private static async Task<(int ExitCode, string Output)> RunDockerAsync(
        string[] arguments,
        string? workingDirectory,
        IEnumerable<string> syntheticKeys)
    {
        var start = new ProcessStartInfo("docker")
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            WorkingDirectory = workingDirectory ?? Environment.CurrentDirectory,
        };

        foreach (var argument in arguments)
        {
            start.ArgumentList.Add(argument);
        }

        foreach (var key in syntheticKeys)
        {
            start.Environment.Remove(key);
        }

        start.Environment.Remove("COMPOSE_FILE");
        start.Environment.Remove("COMPOSE_ENV_FILES");
        start.Environment.Remove("COMPOSE_PROJECT_NAME");

        using var process = Process.Start(start)!;
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(5));
        var output = process.StandardOutput.ReadToEndAsync(timeout.Token);
        var error = process.StandardError.ReadToEndAsync(timeout.Token);

        try
        {
            await process.WaitForExitAsync(timeout.Token);
            await error;

            return (process.ExitCode, await output);
        }
        catch (OperationCanceledException)
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }

            throw;
        }
    }
}
