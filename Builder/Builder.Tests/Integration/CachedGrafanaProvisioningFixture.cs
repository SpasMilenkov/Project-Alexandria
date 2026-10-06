using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Builder.Services;
using Builder.Tests.Services;

namespace Builder.Tests.Integration;

internal sealed class CachedGrafanaProvisioningFixture : IDisposable
{
    private readonly ConfigurationArtifactFixture _artifacts = new(monitoring: true);
    private readonly ProcessRunner _runner = new();
    private readonly CancellationToken _ct = TestContext.Current.CancellationToken;
    private readonly string _project = "builder-grafana-" + Guid.NewGuid().ToString("N");
    private readonly SecretRedactor _redactor;
    private bool _started;
    private string _username = "";
    private string _password = "";

    public CachedGrafanaProvisioningFixture()
    {
        _redactor = new SecretRedactor(() => _artifacts.Context.Config.EnvironmentValues);
    }

    public void Prepare()
    {
        Require(Environment.GetEnvironmentVariable("ALEXANDRIA_BUILDER_DOCKER_TESTS") == "1", "Cached Docker tests require explicit opt-in.");
        _artifacts.Context.Config.ProjectName = _project;
        _artifacts.Context.Config.SourceRoot = ConfigurationArtifactFixture.FindRepositoryRoot();
        var config = _artifacts.Resolve();
        _artifacts.Generate(config);

        var generated = Compose("docker-compose.yml", ["config", "--format", "json"]);
        RequireSuccess(generated, "parse generated monitoring configuration");
        var graph = JsonNode.Parse(generated.StandardOutput)!;
        var grafana = graph["services"]!["grafana"]!.DeepClone();
        grafana.AsObject().Remove("depends_on");
        grafana["networks"] = new JsonObject { ["observability-net"] = new JsonObject() };
        var image = grafana["image"]!.GetValue<string>();
        Require(Docker(["image", "inspect", "--format", "{{.Id}}", image]).Succeeded,
            "Required cached Grafana image is unavailable: " + image + ". This test never builds or pulls images.");
        Require(grafana["ports"] is null, "Grafana verification must publish no host ports.");
        Require(grafana["container_name"] is null, "Grafana verification must use project-scoped container identities.");

        foreach (var resource in new[] { "ps", "volume", "network" })
        {
            var arguments = resource == "ps"
                ? new[] { "ps", "--all", "--quiet", "--filter", "label=com.docker.compose.project=" + _project }
                : new[] { resource, "ls", "--quiet", "--filter", "label=com.docker.compose.project=" + _project };
            var owned = Docker(arguments);
            RequireSuccess(owned, "check disposable project ownership");
            Require(string.IsNullOrWhiteSpace(owned.StandardOutput), "Disposable Grafana project already has resources.");
        }

        _username = config.EnvironmentValues["GRAFANA_USER"];
        _password = config.EnvironmentValues["GRAFANA_PASSWORD"];
        var isolated = new JsonObject
        {
            ["name"] = _project,
            ["services"] = new JsonObject { ["grafana"] = grafana },
            ["networks"] = new JsonObject { ["observability-net"] = new JsonObject { ["internal"] = true } },
            ["volumes"] = new JsonObject { ["grafana_local_data"] = new JsonObject() },
        };
        ProtectedFile.WriteAtomic(Path.Combine(_artifacts.Target, "grafana.fixture.json"),
            isolated.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
    }

    public async Task StartAsync()
    {
        _started = true;
        RequireSuccess(Compose("grafana.fixture.json", ["up", "--detach", "--pull", "never", "--no-build"]), "start disposable Grafana");

        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(60));
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(_ct, deadline.Token);

        while (!linked.IsCancellationRequested)
        {
            var status = Status();
            var healthy = status.Succeeded && DockerJson.Objects(status.StandardOutput)
                .Any(container => DockerJson.Text(container, "Health") == "healthy");

            if (healthy && Request("api/dashboards/uid/alexandria-overview").Succeeded) return;

            await Task.Delay(500, linked.Token);
        }

        throw new InvalidOperationException("Disposable Grafana did not become ready within 60 seconds.");
    }

    public ProcessResult Request(string path, bool authenticated = true)
    {
        var command = new List<string> { "exec", "--no-TTY", "grafana", "wget", "--timeout=3", "-qO-" };

        if (authenticated)
        {
            command.Add("--header");
            command.Add("Authorization: Basic " + Convert.ToBase64String(Encoding.UTF8.GetBytes(_username + ":" + _password)));
        }

        command.Add("http://localhost:3000/monitoring/" + path);
        return Compose("grafana.fixture.json", command);
    }

    public ProcessResult Status() => Compose("grafana.fixture.json", ["ps", "--all", "--format", "json"]);

    public void RequireSuccess(ProcessResult result, string operation)
    {
        Require(result.Succeeded, "Could not " + operation + ": " + _redactor.Redact(result.StandardError));
    }

    private ProcessResult Compose(string filename, IReadOnlyList<string> arguments, CancellationToken? ct = null) => Docker(
        ["compose", "--project-name", _project, "--project-directory", _artifacts.Target,
            "--env-file", Path.Combine(_artifacts.Target, ".env"), "--file", Path.Combine(_artifacts.Target, filename), .. arguments], ct);

    private ProcessResult Docker(IReadOnlyList<string> arguments, CancellationToken? ct = null)
    {
        var start = new ProcessStartInfo("docker")
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            WorkingDirectory = _artifacts.Target,
        };

        foreach (var argument in arguments) start.ArgumentList.Add(argument);

        return _runner.Run(start, TimeSpan.FromSeconds(30), ct ?? _ct);
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    public void Dispose()
    {
        try
        {
            if (_started)
            {
                RequireSuccess(Compose("grafana.fixture.json", ["down", "--volumes", "--remove-orphans"], CancellationToken.None),
                    "clean up disposable Grafana");
            }
        }
        finally
        {
            _artifacts.Dispose();
        }
    }
}
