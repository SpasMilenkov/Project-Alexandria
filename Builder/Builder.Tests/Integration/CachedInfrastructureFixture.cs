using System.Diagnostics;
using System.Text.Json;
using Builder.Models;
using Builder.Services;
using Builder.Workflow;

namespace Builder.Tests.Integration;

internal sealed class CachedInfrastructureFixture : IDisposable
{
    internal static readonly string[] CoreServices = ["postgres", "rabbitmq-config", "rabbitmq", "garage", "garage-init"];
    internal static readonly string[] OneShotServices = ["rabbitmq-config", "garage-init"];

    private static readonly string[] Images =
    [
        "postgres:16-alpine", "rabbitmq:4.2.5-management-alpine", "dxflrs/garage:v2.2.0",
        "alpine:3.20", "alexandria-local-garage-init:latest",
    ];

    private readonly ProcessRunner _runner = new();
    private readonly Dictionary<string, string> _secrets = new();
    private readonly SecretRedactor _redactor;
    private readonly List<string> _capturedOutput = [];
    private readonly ConfigurationService _configuration = new(new TemplateService("Local"));
    private readonly CancellationToken _cancellationToken = TestContext.Current.CancellationToken;
    private bool _ownsPlan;
    private bool _started;

    public CachedInfrastructureFixture()
    {
        Require(Environment.GetEnvironmentVariable("ALEXANDRIA_BUILDER_DOCKER_TESTS") == "1",
            "Docker integration tests require explicit opt-in.");

        var identity = Guid.NewGuid().ToString("N");
        Target = Path.Combine(Path.GetTempPath(), "alexandria-builder-infrastructure-" + identity);
        Context = new InstallationContext
        {
            InstallPath = Target,
            SourceRoot = FindSourceRoot(),
            AdminAccount = new AdminAccountInput
            {
                Username = "fixtureadmin",
                Email = "fixture@example.invalid",
                Password = new CredentialService().GeneratePassword() + "!1aA",
            },
        };
        Context.Config.ProjectName = "builder-infra-" + identity;
        Context.Config.PublicEndpoint = new PublicEndpoint { Host = "192.0.2.10", HttpsPort = 443 };
        Context.Config.Ports = new Dictionary<string, int> { ["HTTP_PORT"] = 80, ["HTTPS_PORT"] = 443 };
        InstallationConfigurationResolver.Resolve(Context, new CredentialService());
        _redactor = new SecretRedactor(() => _secrets);
        RememberSecrets(Context.Config.EnvironmentValues);
    }

    public string Target { get; }
    public InstallationContext Context { get; }
    public SourceImageFixture? SourceImages { get; } = SourceImageFixture.FromEnvironment();
    public string HandoffPath => Path.Combine(Target, "init-output", "garage-credentials.env");

    public void Prepare()
    {
        foreach (var image in Images)
        {
            if (SourceImages is not null && image == "alexandria-local-garage-init:latest") continue;

            var result = Docker(["image", "inspect", "--format", "{{.Id}}", image]);
            Require(result.Succeeded, $"Required cached image '{image}' is unavailable. This suite never pulls or builds images.");
        }

        SourceImages?.ValidateImages(["garage-init"], arguments => Docker(arguments));

        foreach (var arguments in new[]
        {
            new[] { "ps", "--all", "--quiet", "--filter", "label=com.docker.compose.project=" + Context.Config.ProjectName },
            new[] { "volume", "ls", "--quiet", "--filter", "label=com.docker.compose.project=" + Context.Config.ProjectName },
            new[] { "network", "ls", "--quiet", "--filter", "label=com.docker.compose.project=" + Context.Config.ProjectName },
        })
        {
            var result = Docker(arguments);
            RequireSuccess(result, "inspect fixture ownership");
            Require(string.IsNullOrWhiteSpace(result.StandardOutput), "The disposable project already has Docker resources; refusing to use them.");
        }

        _configuration.BeginGeneration(Context.Config, Target);

        if (!OperatingSystem.IsWindows())
        {
            File.SetUnixFileMode(Target, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        }

        EnvGenerator.WriteFile(Path.Combine(Target, ".env"), Context.Config.EnvironmentValues);
        _configuration.WriteAllConfigFiles(Context.Config, Target);
        WriteOverride();

        var rendered = Compose(["config", "--format", "json"]);
        RequireSuccess(rendered, "validate generated Compose");

        using var document = JsonDocument.Parse(rendered.StandardOutput);
        var services = document.RootElement.GetProperty("services");

        foreach (var service in CoreServices)
        {
            Require(services.TryGetProperty(service, out var definition), "Generated infrastructure is missing a required service.");
            Require(!definition.TryGetProperty("container_name", out _), "Disposable infrastructure must use project-scoped container names.");
            Require(!definition.TryGetProperty("ports", out var ports) || ports.GetArrayLength() == 0,
                "Infrastructure integration tests must not publish host ports.");
        }

        Require(!services.GetProperty("garage-init").TryGetProperty("build", out _),
            "The cached helper override must remove its source build definition.");
        SourceImages?.ValidateCompose(services, ["garage-init"]);

        foreach (var kind in new[] { "volumes", "networks" })
        {
            foreach (var resource in document.RootElement.GetProperty(kind).EnumerateObject())
            {
                Require(!resource.Value.TryGetProperty("external", out var external) || external.ValueKind == JsonValueKind.False,
                    "The fixture must not use external Docker resources.");
                Require(resource.Value.GetProperty("name").GetString()!.StartsWith(Context.Config.ProjectName + "_", StringComparison.Ordinal),
                    "The fixture must own every named Docker resource in its composition.");
            }
        }

        _ownsPlan = true;
    }

    public IReadOnlyList<ContainerStatus> Start()
    {
        Require(_ownsPlan, "Validate disposable resource ownership before starting infrastructure.");
        _started = true;
        var result = Compose(["up", "--detach", "--pull", "never", "--no-build", .. CoreServices], TimeSpan.FromMinutes(5));
        RequireSuccess(result, "start disposable infrastructure");
        return WaitForReady();
    }

    public IReadOnlyList<ContainerStatus> WaitForReady()
    {
        var docker = new DockerService();
        docker.Configure(Context.Config, _cancellationToken);
        var ready = docker.WaitForServices(Target, CoreServices, TimeSpan.FromMinutes(5), OneShotServices,
            onOutput: Capture);
        var statuses = docker.GetServiceStatuses(Target);
        Require(ready, "Disposable infrastructure did not initialize: " + string.Join("; ", statuses.Select(status => status.Display)));

        return statuses;
    }

    public void RepeatInitialization(bool forceCredentialExport = false)
    {
        ProcessResult result;

        if (forceCredentialExport)
        {
            result = Compose(["run", "--rm", "--pull", "never", "--no-deps", "--env", "GARAGE_FORCE_CREDENTIAL_EXPORT=true", "garage-init"],
                TimeSpan.FromMinutes(5));
        }
        else
        {
            result = Compose(["up", "--detach", "--force-recreate", "--pull", "never", "--no-build", "--no-deps", "garage-init"],
                TimeSpan.FromMinutes(5));
        }

        RequireSuccess(result, "repeat storage initialization");
        WaitForReady();
        RememberHandoffSecrets();
    }

    public void VerifyStorageCredentials()
    {
        const string script = """
            set -eu
            . /output/garage-credentials.env
            export AWS_DEFAULT_REGION=garage
            export AWS_ACCESS_KEY_ID="$GARAGE_S3_ACCESS_KEY" AWS_SECRET_ACCESS_KEY="$GARAGE_S3_SECRET_KEY"
            aws --endpoint-url http://garage:3900 s3api get-bucket-cors --bucket alexandria-files >/dev/null
            export AWS_ACCESS_KEY_ID="$GARAGE_PREVIEW_KEY_ID" AWS_SECRET_ACCESS_KEY="$GARAGE_PREVIEW_SECRET"
            aws --endpoint-url http://garage:3900 s3api list-objects-v2 --bucket alexandria-previews --max-keys 0 >/dev/null
            export AWS_ACCESS_KEY_ID="$GARAGE_STREAMING_KEY_ID" AWS_SECRET_ACCESS_KEY="$GARAGE_STREAMING_SECRET"
            aws --endpoint-url http://garage:3900 s3api list-objects-v2 --bucket alexandria-streaming --max-keys 0 >/dev/null
            """;
        var result = Compose(["run", "--rm", "--pull", "never", "--no-deps", "--entrypoint", "/bin/sh", "garage-init", "-c", script],
            TimeSpan.FromMinutes(1));
        RequireSuccess(result, "verify real storage credentials and CORS");
    }

    public void VerifyDatabaseAndBroker()
    {
        const string databaseProbe = """
            set -eu
            export PGPASSWORD="$POSTGRES_PASSWORD"
            psql --host 127.0.0.1 --username "$POSTGRES_USER" --dbname "$POSTGRES_DB" --no-password \
                --tuples-only --no-align --command "SELECT current_database(), current_user; SELECT pg_has_role('postgres_exporter', 'pg_monitor', 'member');"
            """;
        var database = Compose(["exec", "--no-TTY", "postgres", "/bin/sh", "-c", databaseProbe]);
        RequireSuccess(database, "authenticate to the initialized database");
        var expected = Context.Config.EnvironmentValues["DB_NAME"] + "|" + Context.Config.EnvironmentValues["DB_USERNAME"];
        var rows = database.StandardOutput.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        Require(rows.Contains(expected) && rows.Contains("t"), "The real database identity or exporter privileges disagree with the generated settings.");

        var broker = Compose(["exec", "--no-TTY", "rabbitmq", "rabbitmqctl", "list_users", "--formatter", "json"]);
        RequireSuccess(broker, "inspect initialized broker users");
        using var users = JsonDocument.Parse(broker.StandardOutput);
        var names = users.RootElement.EnumerateArray().Select(user => user.GetProperty("user").GetString()).ToHashSet(StringComparer.Ordinal);
        var required = new[]
        {
            Context.Config.EnvironmentValues["RABBITMQ_USER"], "api_user", "document_worker_user", "media_worker_user",
            "media_transpilation_worker", "lyrics_worker_user", "media_metadata_worker_user", "playlist_worker_user", "essentia_worker_user",
        };
        Require(required.All(names.Contains), "The initialized broker is missing a rendered application identity.");
    }

    public GarageCredentialSet ReadHandoff()
    {
        Require(File.Exists(HandoffPath), "Storage initialization did not publish a complete key handoff.");
        var content = File.ReadAllText(HandoffPath);
        var exported = EnvGenerator.Parse(content);
        RememberSecrets(exported);
        Require(exported.GetValueOrDefault("ALEXANDRIA_GENERATION_ID") == Context.Config.GenerationId,
            "Storage initialization published a stale generation handoff.");

        var keys = GarageCredentialsFileParser.Parse(content);
        Require(keys is not null && keys.IsComplete, "Storage initialization published incomplete keys.");
        Require(!Directory.EnumerateFiles(Path.GetDirectoryName(HandoffPath)!, ".garage-credentials.*").Any(),
            "Storage initialization left a partial handoff behind.");

        if (!OperatingSystem.IsWindows())
        {
            Require(File.GetUnixFileMode(HandoffPath) == (UnixFileMode.UserRead | UnixFileMode.UserWrite),
                "The storage handoff must have mode 0600.");
            Require(ReadOwner(HandoffPath) == ReadOwner(Path.Combine(Target, "garage.toml")),
                "The storage handoff must belong to the installation settings owner.");
        }

        return keys!;
    }

    public void CommitHandoff()
    {
        InstallationOrchestrator.PersistStorageKeys(Context, Capture);
        RememberSecrets(Context.Config.EnvironmentValues);
        Require(!File.Exists(HandoffPath), "The successfully committed storage handoff was not removed.");
        Require(GarageCredentialPersistence.HasPersistedKeys(Context), "The committed environment has incomplete storage credentials.");
        Context.Config.Stage = "storage-ready";
        _configuration.SaveConfiguration(Context.Config, Target);
    }

    public InstallationContext Reload()
    {
        var saved = _configuration.LoadConfiguration(Target);
        Require(saved is not null, "The installation checkpoint was not persisted.");
        var resumed = new InstallationContext { Config = saved! };
        Require(!InstallationConfigurationResolver.Inspect(resumed).IsIncompleteGeneration,
            "The committed storage checkpoint must match the persisted environment and generated artifacts.");
        InstallationConfigurationResolver.Resolve(resumed, new CredentialService());

        return resumed;
    }

    public string CoreContainerIds()
    {
        var result = Compose(["ps", "--all", "--quiet", .. CoreServices]);
        RequireSuccess(result, "inspect disposable container identities");

        return string.Join("\n", result.StandardOutput.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Order(StringComparer.Ordinal));
    }

    public void VerifyInitLogsContainNoKeys(GarageCredentialSet keys)
    {
        var logs = Compose(["logs", "--no-color", "garage-init"]);
        RequireSuccess(logs, "read storage initialization logs");
        var output = string.Join("\n", _capturedOutput.Append(logs.StandardOutput).Append(logs.StandardError));
        var leaked = output.Split('\n').FirstOrDefault(line => GarageCredentialsFileParser.ToEnvValues(keys).Values
            .Any(value => line.Contains(value, StringComparison.Ordinal)));
        Require(leaked is null, "Storage initialization output disclosed generated credential material: " + _redactor.Redact(leaked ?? string.Empty));
    }

    public ProcessResult Compose(IReadOnlyList<string> arguments, TimeSpan? timeout = null, CancellationToken? cancellationToken = null)
    {
        var command = new[]
        {
            "compose", "--project-name", Context.Config.ProjectName, "--project-directory", Target,
            "-f", Path.Combine(Target, "docker-compose.yml"), "-f", Path.Combine(Target, "docker-compose.fixture.yml"),
            "--env-file", Path.Combine(Target, ".env"),
        }.Concat(arguments).ToArray();

        return Docker(command, timeout, cancellationToken);
    }

    public static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    private ProcessResult Docker(IReadOnlyList<string> arguments, TimeSpan? timeout = null, CancellationToken? cancellationToken = null)
    {
        var start = new ProcessStartInfo("docker")
        {
            WorkingDirectory = Context.SourceRoot,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        foreach (var argument in arguments) start.ArgumentList.Add(argument);

        var result = _runner.Run(start, timeout ?? TimeSpan.FromSeconds(30), cancellationToken ?? _cancellationToken);
        Capture(result.StandardOutput);
        Capture(result.StandardError);

        return result;
    }

    private void RequireSuccess(ProcessResult result, string operation)
    {
        if (!result.Succeeded)
        {
            RememberHandoffSecrets();
            var diagnostic = _redactor.Redact(result.StandardError);
            throw new InvalidOperationException($"Could not {operation} (exit {result.ExitCode}, timed out: {result.TimedOut}). {diagnostic}");
        }
    }

    private void RememberHandoffSecrets()
    {
        if (File.Exists(HandoffPath)) RememberSecrets(EnvGenerator.Parse(File.ReadAllText(HandoffPath)));
    }

    private void RememberSecrets(IReadOnlyDictionary<string, string> values)
    {
        foreach (var (key, value) in values) _secrets[key] = value;
    }

    private void Capture(string text) => _capturedOutput.Add(text);

    private string ReadOwner(string path)
    {
        var start = new ProcessStartInfo("stat")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        start.ArgumentList.Add("-c");
        start.ArgumentList.Add("%u:%g");
        start.ArgumentList.Add(path);
        var result = _runner.Run(start, TimeSpan.FromSeconds(5), _cancellationToken);
        RequireSuccess(result, "inspect handoff ownership");

        return result.StandardOutput.Trim();
    }

    private void WriteOverride()
    {
        if (SourceImages is not null)
        {
            var text = new System.Text.StringBuilder("services:\n");
            SourceImages.AppendOverride(text, "garage-init");
            ProtectedFile.WriteAtomic(Path.Combine(Target, "docker-compose.fixture.yml"), text.ToString());
            return;
        }

        var script = Path.Combine(Context.SourceRoot, "scripts", "garage-init", "init.sh")
            .Replace(Path.DirectorySeparatorChar, '/').Replace("$", "$$", StringComparison.Ordinal).Replace("'", "''", StringComparison.Ordinal);
        ProtectedFile.WriteAtomic(Path.Combine(Target, "docker-compose.fixture.yml"), $$"""
            services:
              garage-init:
                build: !reset null
                image: alexandria-local-garage-init:latest
                pull_policy: never
                volumes:
                  - '{{script}}:/scripts/init.sh:ro'
            """);
    }

    private static string FindSourceRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);

        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "scripts", "garage-init", "init.sh")) &&
                Directory.Exists(Path.Combine(current.FullName, "Builder", "Templates", "Local"))) return current.FullName;

            current = current.Parent;
        }

        throw new InvalidOperationException("Run these integration tests from a source checkout containing the Builder templates and Garage init script.");
    }

    public void Dispose()
    {
        try
        {
            if (_started && _ownsPlan)
            {
                var result = Compose(["down", "--volumes", "--timeout", "10"], TimeSpan.FromMinutes(1), CancellationToken.None);
                RequireSuccess(result, "remove this fixture's disposable Docker resources");
            }

            if (Directory.Exists(Target)) Directory.Delete(Target, recursive: true);
        }
        finally
        {
            Context.Cancellation.Dispose();
        }
    }
}
