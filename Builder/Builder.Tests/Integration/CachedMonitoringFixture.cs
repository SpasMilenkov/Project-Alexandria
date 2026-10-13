using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Builder.Models;
using Builder.Services;

namespace Builder.Tests.Integration;

internal sealed class CachedMonitoringFixture : IDisposable
{
    internal static readonly string[] MonitoringServices =
    [
        "prometheus", "grafana", "cadvisor", "postgres-exporter", "alloy", "loki",
    ];

    private readonly CachedInfrastructureFixture _infrastructure = new();
    private readonly ProcessRunner _runner = new();
    private readonly CancellationToken _ct = TestContext.Current.CancellationToken;
    private readonly SecretRedactor _redactor;
    private readonly string _foreignName = "builder-foreign-log-" + Guid.NewGuid().ToString("N");
    private bool _started;
    private bool _foreignStarted;
    private string _alpineImage = "";

    public CachedMonitoringFixture()
    {
        _infrastructure.Context.Config.Features = new FeatureSelection();
        _infrastructure.Context.Features.SetEnabled(FeatureCatalog.Monitoring.Id, true);
        InstallationConfigurationResolver.Resolve(_infrastructure.Context, new CredentialService());
        _redactor = new SecretRedactor(() => _infrastructure.Context.Config.EnvironmentValues);
    }

    public string Marker { get; } = "monitoring-probe-" + Guid.NewGuid().ToString("N");
    public string SourceRoot => _infrastructure.Context.SourceRoot;
    public string Project => _infrastructure.Context.Config.ProjectName;

    public void Prepare()
    {
        var output = Path.Combine(SourceRoot, "Backend", "Alexandria.Api", "bin", "Debug", "net10.0");
        Require(_infrastructure.SourceImages is not null || File.Exists(Path.Combine(output, "Alexandria.Api.dll")),
            "Current compiled API output is required. Build the API with existing cached prerequisites before this test.");
        var template = File.ReadAllText(Path.Combine(SourceRoot, "Builder", "Templates", "Local", "docker-compose", "monitoring.yml.template"));

        foreach (Match match in Regex.Matches(template, @"(?m)^\s+image:\s*(\S+)"))
        {
            var image = match.Groups[1].Value;
            Require(image.Contains("@sha256:", StringComparison.Ordinal), "Every monitoring image must be pinned by digest.");
            Require(Docker(["image", "inspect", "--format", "{{.Id}}", image]).Succeeded,
                "Required cached monitoring image is unavailable: " + image + ". This test never builds or pulls images.");
        }

        _infrastructure.Prepare();
        var apiOverride = new StringBuilder();

        if (_infrastructure.SourceImages is not null)
        {
            _infrastructure.SourceImages.ValidateImages(["api"], arguments => Docker(arguments));
            _infrastructure.SourceImages.AppendOverride(apiOverride, "api");
        }
        else
        {
            var api = Docker(["image", "inspect", "--format", "{{.Id}}", "alexandria-local-api:latest"]);
            RequireSuccess(api, "inspect cached API runtime");
            var apiSettings = Path.Combine(_infrastructure.Target, "monitoring-api-appsettings.json");
            ProtectedFile.WriteAtomic(apiSettings, "{}", UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.GroupRead | UnixFileMode.OtherRead);
            apiOverride.AppendLine($$"""
                  api:
                    build: !reset null
                    image: {{api.StandardOutput.Trim()}}
                    pull_policy: never
                    entrypoint: ["dotnet", "Alexandria.Api.dll"]
                    environment:
                      ASPNETCORE_CONTENTROOT: /fixture-config
                    volumes:
                      - {{Yaml(output + ":/app:ro")}}
                      - {{Yaml(apiSettings + ":/fixture-config/appsettings.json:ro")}}
                """);
        }

        var alpine = Docker(["image", "inspect", "--format", "{{index .RepoDigests 0}}", "alpine:3.20"]);
        RequireSuccess(alpine, "inspect cached Compose helper runtime");
        _alpineImage = alpine.StandardOutput.Trim();

        var script = "while true; do printf '%s\\n' '[12:00:00 ERR] " + Marker +
            "-plain' '{\"Level\":\"Information\",\"RenderedMessage\":\"" + Marker +
            "-json\",\"Properties\":{\"SourceContext\":\"MonitoringProbe\"}}'; sleep 2; done";
        ProtectedFile.WriteAtomic(Path.Combine(_infrastructure.Target, "monitoring.fixture.yml"), $$"""
            services:
            {{apiOverride.ToString().TrimEnd()}}
              monitoring-log-probe:
                image: {{_alpineImage}}
                pull_policy: never
                networks: [observability-net]
                labels:
                  logs.collect: "true"
                command: ["/bin/sh", "-c", {{JsonSerializer.Serialize(script, new JsonSerializerOptions())}}]
            """);

        if (_infrastructure.SourceImages is not null)
        {
            var composition = Compose(["config", "--format", "json"]);
            RequireSuccess(composition, "validate source-image monitoring composition");
            using var parsed = JsonDocument.Parse(composition.StandardOutput);
            _infrastructure.SourceImages.ValidateCompose(parsed.RootElement.GetProperty("services"), ["api", "garage-init"]);
        }
    }

    public void Start()
    {
        _infrastructure.Start();
        _infrastructure.CommitHandoff();
        _started = true;
        var initialServices = MonitoringServices.Where(service => service != "alloy").ToArray();
        RequireSuccess(Compose(["up", "--detach", "--pull", "never", "--no-build", "--no-deps", "api", .. initialServices, "monitoring-log-probe"], TimeSpan.FromSeconds(60)),
            "start isolated monitoring and current API");
        var foreignScript = "while true; do printf '%s\\n' '[12:00:00 ERR] " + Marker + "-foreign'; sleep 2; done";
        _foreignStarted = true;
        RequireSuccess(Docker(["run", "--detach", "--pull", "never", "--name", _foreignName, "--network", Project + "_observability-net",
            "--label", "com.docker.compose.project=" + _foreignName, "--label", "com.docker.compose.service=foreign-probe",
            "--label", "logs.collect=true", _alpineImage, "/bin/sh", "-c", foreignScript]), "start isolated foreign-project log probe");
        RequireSuccess(Compose(["up", "--detach", "--pull", "never", "--no-build", "--no-deps", "alloy"]),
            "start log discovery after both synthetic projects exist");
    }

    public ProcessResult Compose(IReadOnlyList<string> arguments, TimeSpan? timeout = null, CancellationToken? ct = null) =>
        _infrastructure.Compose(["--file", Path.Combine(_infrastructure.Target, "monitoring.fixture.yml"), .. arguments], timeout, ct);

    public ProcessResult Request(string service, string path, bool grafanaAuth = false)
    {
        var authority = service switch
        {
            "prometheus" => "prometheus:9090",
            "grafana" => "grafana:3000/monitoring",
            "loki" => "loki:3100",
            "api" => "api:8080",
            _ => throw new ArgumentOutOfRangeException(nameof(service)),
        };
        var arguments = new List<string> { "exec", "--no-TTY", "grafana", "wget", "--timeout=3", "-qO-" };

        if (grafanaAuth)
        {
            var values = _infrastructure.Context.Config.EnvironmentValues;
            arguments.Add("--header");
            arguments.Add("Authorization: Basic " + Convert.ToBase64String(Encoding.UTF8.GetBytes(values["GRAFANA_USER"] + ":" + values["GRAFANA_PASSWORD"])));
        }

        arguments.Add("http://" + authority + "/" + path);
        return Compose(arguments);
    }

    public async Task WaitFor(Func<bool> condition, string description, TimeSpan? timeout = null)
    {
        var deadline = DateTime.UtcNow + (timeout ?? TimeSpan.FromSeconds(90));

        while (DateTime.UtcNow < deadline)
        {
            _ct.ThrowIfCancellationRequested();
            if (condition()) return;
            await Task.Delay(1000, _ct);
        }

        var status = Compose(["ps", "--all", "--format", "json"]);
        var details = string.Join("; ", DockerJson.Objects(status.StandardOutput)
            .Select(container => DockerJson.Text(container, "Service") + ": " + DockerJson.Text(container, "State") + " " + DockerJson.Text(container, "Health")));

        throw new InvalidOperationException("Timed out waiting for " + description + ". " + _redactor.Redact(details));
    }

    public void RequireSuccess(ProcessResult result, string operation) => Require(result.Succeeded,
        "Could not " + operation + ": " + _redactor.Redact(result.StandardError));

    private ProcessResult Docker(IReadOnlyList<string> arguments, CancellationToken? ct = null)
    {
        var start = new ProcessStartInfo("docker")
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            WorkingDirectory = SourceRoot,
        };

        foreach (var argument in arguments) start.ArgumentList.Add(argument);

        return _runner.Run(start, TimeSpan.FromSeconds(30), ct ?? _ct);
    }

    private static string Yaml(string value) => "'" + value.Replace("$", "$$", StringComparison.Ordinal).Replace("'", "''", StringComparison.Ordinal) + "'";

    private static void Require(bool condition, string description)
    {
        if (!condition) throw new InvalidOperationException(description);
    }

    public void Dispose()
    {
        try
        {
            if (_foreignStarted) RequireSuccess(Docker(["rm", "--force", _foreignName], CancellationToken.None), "remove synthetic foreign-project log probe");
        }
        finally
        {
            try
            {
                if (_started)
                {
                    RequireSuccess(Compose(["down", "--volumes", "--remove-orphans", "--timeout", "10"], TimeSpan.FromSeconds(60), CancellationToken.None),
                        "remove this fixture's monitoring resources");
                }
            }
            finally
            {
                _infrastructure.Dispose();
            }
        }
    }
}
