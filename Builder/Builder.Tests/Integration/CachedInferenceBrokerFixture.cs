using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.Json;
using Builder.Models;
using Builder.Services;

namespace Builder.Tests.Integration;

internal sealed class CachedInferenceBrokerFixture : IDisposable
{
    private const string Coordinator = "media-metadata-worker";
    private const string CoordinatorProject = "Alexandria.Workers.MediaMetadata";
    private readonly CachedWorkerProcessingFixture _workers;
    private readonly string _backbone;
    private readonly CancellationToken _ct = TestContext.Current.CancellationToken;

    public CachedInferenceBrokerFixture(string backbone)
    {
        _backbone = backbone;
        _workers = new CachedWorkerProcessingFixture(configure: context =>
        {
            context.Config.Features.SetAudioBackbone(backbone);
            context.Config.Features.Enabled.Add(FeatureCatalog.Autotagging.Id);
            context.Config.ModelSourceDirectory = ExistingModels();
        });
    }

    private string Classifier => "essentia-" + _backbone + "-worker";

    public void PrepareAndStart()
    {
        _workers.PrepareAndStart();
        var source = _workers.Context.SourceRoot;
        var output = Path.Combine(source, "Backend", CoordinatorProject, "bin", "Debug", "net10.0");
        Require(_workers.SourceImages is not null || File.Exists(Path.Combine(output, CoordinatorProject + ".dll")),
            "Build the current metadata coordinator using cached packages before opting in.");

        _workers.SourceImages?.ValidateImages([Coordinator, Classifier], arguments => Docker(arguments));

        var preparation = new ModelPreparationService();
        var plan = preparation.CreatePlan(source, _workers.Target, _backbone, ExistingModels());
        var prepared = preparation.PrepareAsync(plan, cancellationToken: _ct).GetAwaiter().GetResult();
        Require(plan.Assets.Count == 17 && prepared.PreparedFiles == 17,
            "The fresh selected cache must contain exactly its 17 verified model assets.");

        var overlay = CreateRuntimeOverlay(source, output);
        var overridePath = Path.Combine(_workers.Target, "docker-compose.fixture.yml");
        ProtectedFile.WriteAtomic(overridePath, File.ReadAllText(overridePath) + "\n" + overlay);
        var composition = Compose(["config", "--format", "json"]);
        RequireSuccess(composition, "validate the current classifier/coordinator overlay");
        using var parsed = JsonDocument.Parse(composition.StandardOutput);
        _workers.SourceImages?.ValidateCompose(parsed.RootElement.GetProperty("services"), [Coordinator, Classifier]);

        foreach (var service in new[] { Coordinator, Classifier })
        {
            var definition = parsed.RootElement.GetProperty("services").GetProperty(service);
            Require(!definition.TryGetProperty("build", out _), "Inference acceptance must not build images.");
            Require(!definition.TryGetProperty("ports", out var ports) || ports.GetArrayLength() == 0,
                "Inference acceptance must not publish host ports.");
        }

        RequireSuccess(Compose(["up", "--detach", "--pull", "never", "--no-build", "--no-deps", Coordinator, Classifier]),
            "start the isolated current inference workers from cached runtimes");
        WaitUntil(() => Compose(["exec", "--no-TTY", Coordinator, "dotnet", CoordinatorProject + ".dll", "--healthcheck"]).Succeeded,
            "coordinator readiness", TimeSpan.FromMinutes(1));
        WaitUntil(() =>
        {
            var status = Compose(["ps", "--all", "--format", "json", Coordinator]);
            RequireSuccess(status, "inspect the actual coordinator Docker healthcheck");
            return DockerJson.Objects(status.StandardOutput).Any(row => DockerJson.Text(row, "Health") == "healthy");
        }, "coordinator Docker healthcheck", TimeSpan.FromMinutes(1));
        WaitUntil(() => Queue(ClassifyQueue(_backbone)).GetProperty("consumers").GetInt32() == 1,
            "selected model load and broker consumer readiness", TimeSpan.FromMinutes(3));

        foreach (var service in new[] { Coordinator, Classifier })
        {
            var identity = Compose(["exec", "--no-TTY", service, "/bin/sh", "-c", "printf '%s:%s' \"$(id -u)\" \"$(id -g)\""]);
            RequireSuccess(identity, "inspect actual inference worker identity");
            Require(identity.StandardOutput.Trim() == "1001:1001", "Both inference workers must run as UID/GID 1001.");
        }

        var limit = Compose(["exec", "--no-TTY", Classifier, "cat", "/sys/fs/cgroup/memory.max"]);
        RequireSuccess(limit, "inspect the actual classifier memory limit");
        Require(limit.StandardOutput.Trim() == "2147483648", "The real classifier must retain its 2 GiB cap.");
        var other = _backbone == "effnet" ? "maest" : "effnet";
        Require(Queue(ClassifyQueue(other)).GetProperty("consumers").GetInt32() == 0,
            "The omitted backbone must have no classifier consumer.");
    }

    private StringBuilder CreateRuntimeOverlay(string source, string output)
    {
        if (_workers.SourceImages is not null)
        {
            var text = new StringBuilder();
            _workers.SourceImages.AppendOverride(text, Coordinator);
            text.AppendLine("    environment:");
            text.AppendLine("      Essentia__BatchSize: \"1\"");
            text.AppendLine("      Essentia__MaxBatchWaitSeconds: \"1\"");
            _workers.SourceImages.AppendOverride(text, Classifier);
            text.AppendLine("    read_only: true");
            text.AppendLine("    tmpfs:");
            text.AppendLine("      - /tmp:rw,nosuid,size=64m");
            return text;
        }

        var settings = Path.Combine(_workers.Target, "coordinator-settings.json");
        ProtectedFile.WriteAtomic(settings, "{}", UnixFileMode.UserRead | UnixFileMode.UserWrite |
            UnixFileMode.GroupRead | UnixFileMode.OtherRead);
        var classifierSource = Path.Combine(source, "classificators", "essentia");
        var overlay = new StringBuilder($$"""
          {{Coordinator}}:
            build: !reset null
            image: alexandria-local-media-metadata-worker:latest
            pull_policy: never
            entrypoint: ["dotnet", "{{CoordinatorProject}}.dll"]
            environment:
              ASPNETCORE_CONTENTROOT: /fixture-config
              Essentia__BatchSize: "1"
              Essentia__MaxBatchWaitSeconds: "1"
            volumes:
              - {{Yaml(output + ":/app:ro")}}
              - {{Yaml(settings + ":/fixture-config/appsettings.json:ro")}}
          {{Classifier}}:
            build: !reset null
            image: alexandria-local-essentia-effnet-worker:latest
            pull_policy: never
            read_only: true
            tmpfs:
              - /tmp:rw,nosuid,size=64m
            entrypoint: ["/bin/bash", "/app/entrypoint.sh"]
            command: ["python", "/app/worker.py"]
            volumes:
        """);

        overlay.AppendLine();

        foreach (var name in new[] { "entrypoint.sh", "worker.py", "essentia_core.py", "model_assets.py", "model-requirements.tsv" })
            overlay.AppendLine("      - " + Yaml(Path.Combine(classifierSource, name) + ":/app/" + name + ":ro"));

        overlay.AppendLine("      - " + Yaml(Path.Combine(classifierSource, "models", "models.sha256") + ":/app/models.sha256:ro"));
        return overlay;
    }

    public InferenceBrokerEvidence VerifyChain()
    {
        var audio = _workers.SeedFile("inference.wav", "audio/wav", CreateWave());
        _workers.Publish("media-metadata.enrich", audio.FileId);
        WaitUntil(() =>
        {
            var status = _workers.Sql($$"""
                SELECT j."Status" FROM "EssentiaBatchFiles" f JOIN "EssentiaBatches" b ON b."Id"=f."BatchId"
                JOIN "Jobs" j ON j."Id"=f."JobId"
                WHERE f."FileId"='{{audio.FileId}}';
                """);
            if (status is "Failed" or "Cancelled" || Queue("media-metadata-trigger-parking-queue").GetProperty("messages_ready").GetInt32() > 0)
                throw Failure("The real coordinator/classifier rejected or parked its inference job");

            return status == "Ready" && _workers.Sql($$"""
                SELECT COUNT(*) FROM "EssentiaBatchFiles" f JOIN "EssentiaBatches" b ON b."Id"=f."BatchId"
                WHERE f."FileId"='{{audio.FileId}}' AND b."Status"='Completed';
                """) == "1";
        }, "coordinator dispatch, classifier result and database commit", TimeSpan.FromMinutes(3));

        var batchBackbone = _workers.Sql($$"""
            SELECT b."Backbone" FROM "EssentiaBatches" b JOIN "EssentiaBatchFiles" f ON f."BatchId"=b."Id"
            WHERE f."FileId"='{{audio.FileId}}';
            """);
        Require(batchBackbone.Equals(_backbone, StringComparison.OrdinalIgnoreCase),
            "The persisted batch must use the chosen backbone.");
        Require(_workers.Sql($"SELECT COUNT(*) FROM \"FileEnrichments\" WHERE \"FileId\"='{audio.FileId}';") == "2",
            "A completed inference must persist genre and mood enrichments.");

        var genreAnalyzer = "essentia-genre-" + _backbone;
        using var genre = JsonDocument.Parse(_workers.Sql($"SELECT \"PayloadJson\" FROM \"FileEnrichments\" " +
            $"WHERE \"FileId\"='{audio.FileId}' AND \"Analyzer\"='{genreAnalyzer}';"));
        using var mood = JsonDocument.Parse(_workers.Sql($"SELECT \"PayloadJson\" FROM \"FileEnrichments\" " +
            $"WHERE \"FileId\"='{audio.FileId}' AND \"Analyzer\"='essentia-mood';"));
        var genreVersion = _workers.Sql($"SELECT \"Version\" FROM \"FileEnrichments\" " +
            $"WHERE \"FileId\"='{audio.FileId}' AND \"Analyzer\"='{genreAnalyzer}';");
        var moodVersion = _workers.Sql($"SELECT \"Version\" FROM \"FileEnrichments\" " +
            $"WHERE \"FileId\"='{audio.FileId}' AND \"Analyzer\"='essentia-mood';");

        WaitUntil(() => Compose(["exec", "--no-TTY", Coordinator, "/bin/sh", "-c",
            "test -z \"$(find /data/staged /data/output -type f -print -quit)\""]).Succeeded,
            "coordinator cleanup of shared staging and output files", TimeSpan.FromSeconds(20));
        WaitUntil(() => new[] { "media-metadata-trigger-queue", ClassifyQueue(_backbone), "media-metadata-results-queue" }
            .All(name =>
            {
                var queue = Queue(name);
                return queue.GetProperty("messages_ready").GetInt32() == 0 &&
                    queue.GetProperty("messages_unacknowledged").GetInt32() == 0;
            }), "acknowledgement of trigger, inference and completion deliveries", TimeSpan.FromSeconds(30));

        var logs = Compose(["logs", "--no-color", Classifier]);
        RequireSuccess(logs, "inspect current classifier inference evidence");
        var inferenceOutput = logs.StandardOutput + logs.StandardError;
        Require(inferenceOutput.Contains("1 ok, 0 failed", StringComparison.Ordinal) &&
                inferenceOutput.Contains("Pinned " + _backbone + " model checksums OK.", StringComparison.Ordinal),
            "The current classifier must verify pinned models and successfully process the dispatched file.");
        var peak = Compose(["exec", "--no-TTY", Classifier, "cat", "/sys/fs/cgroup/memory.peak"]);
        RequireSuccess(peak, "read actual classifier memory peak");

        return new InferenceBrokerEvidence(genre.RootElement.GetProperty("backbone").GetString()!,
            genre.RootElement.GetProperty("predictions").GetArrayLength(), mood.RootElement.EnumerateObject().Count(),
            genreVersion, moodVersion, long.Parse(peak.StandardOutput.Trim(), CultureInfo.InvariantCulture));
    }

    private JsonElement Queue(string name)
    {
        var values = _workers.Context.Config.EnvironmentValues;
        var authorization = Convert.ToBase64String(Encoding.UTF8.GetBytes(values["RABBITMQ_USER"] + ":" + values["RABBITMQ_PASSWORD"]));
        var result = Compose(["exec", "--no-TTY", "api", "wget", "-qO-", "--header", "Authorization: Basic " + authorization,
            "http://rabbitmq:15672/api/queues/" + Uri.EscapeDataString(values["RABBITMQ_VHOST"]) + "/" + name]);
        RequireSuccess(result, "inspect the real inference queue");
        using var json = JsonDocument.Parse(result.StandardOutput);
        return json.RootElement.Clone();
    }

    private void WaitUntil(Func<bool> predicate, string operation, TimeSpan timeout)
    {
        var timer = Stopwatch.StartNew();
        while (timer.Elapsed < timeout)
        {
            _ct.ThrowIfCancellationRequested();
            if (predicate()) return;
            Task.Delay(TimeSpan.FromSeconds(1), _ct).GetAwaiter().GetResult();
        }

        throw Failure("Timed out waiting for " + operation);
    }

    private InvalidOperationException Failure(string message)
    {
        var logs = Compose(["logs", "--no-color", "--tail", "60", Coordinator, Classifier]);
        return new InvalidOperationException(message + ". " + Redact(logs.StandardOutput + logs.StandardError));
    }

    private static byte[] CreateWave()
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream, Encoding.ASCII, leaveOpen: true);
        const int samples = 31 * 16000;
        writer.Write(Encoding.ASCII.GetBytes("RIFF"));
        writer.Write(36 + samples * 2);
        writer.Write(Encoding.ASCII.GetBytes("WAVEfmt "));
        writer.Write(16);
        writer.Write((short)1);
        writer.Write((short)1);
        writer.Write(16000);
        writer.Write(32000);
        writer.Write((short)2);
        writer.Write((short)16);
        writer.Write(Encoding.ASCII.GetBytes("data"));
        writer.Write(samples * 2);
        for (var index = 0; index < samples; index++)
            writer.Write((short)(6000 * Math.Sin(2 * Math.PI * 220 * index / 16000) +
                3000 * Math.Sin(2 * Math.PI * 440 * index / 16000)));
        return stream.ToArray();
    }

    private static string ExistingModels()
    {
        var source = Environment.GetEnvironmentVariable("ALEXANDRIA_EXISTING_MODEL_SOURCE");
        Require(!string.IsNullOrWhiteSpace(source) && Directory.Exists(source),
            "Set ALEXANDRIA_EXISTING_MODEL_SOURCE to existing cached model assets; this suite never downloads models or images.");
        return Path.GetFullPath(source!);
    }

    private static string ClassifyQueue(string backbone) => "classify-" + backbone + "-queue";

    private static string Yaml(string value) => "'" + value.Replace("$", "$$", StringComparison.Ordinal).Replace("'", "''", StringComparison.Ordinal) + "'";

    private ProcessResult Compose(IReadOnlyList<string> arguments) => _workers.Compose(arguments);

    private ProcessResult Docker(IReadOnlyList<string> arguments)
    {
        var start = new ProcessStartInfo("docker")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };

        foreach (var argument in arguments) start.ArgumentList.Add(argument);

        return new ProcessRunner().Run(start, TimeSpan.FromSeconds(30), _ct);
    }

    private string Redact(string value) => new SecretRedactor(() => _workers.Context.Config.EnvironmentValues).Redact(value);

    private void RequireSuccess(ProcessResult result, string operation) => Require(result.Succeeded,
        "Could not " + operation + ": " + Redact(result.StandardError));

    private static void Require(bool condition, string message) => CachedInfrastructureFixture.Require(condition, message);

    public void Dispose() => _workers.Dispose();
}

internal sealed record InferenceBrokerEvidence(string Backbone, int GenrePredictions, int MoodHeads,
    string GenreVersion, string MoodVersion, long MemoryPeakBytes);
