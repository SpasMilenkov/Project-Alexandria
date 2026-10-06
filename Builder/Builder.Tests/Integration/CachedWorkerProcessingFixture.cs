using System.Diagnostics;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Builder.Models;
using Builder.Services;
using Builder.Workflow;

namespace Builder.Tests.Integration;

internal sealed class CachedWorkerProcessingFixture : IDisposable
{
    private static readonly (string Service, string Project)[] AllApplications =
    [
        ("api", "Alexandria.Api"),
        ("media-worker", "Alexandria.Workers.Media"),
        ("document-worker", "Alexandria.Workers.Document"),
        ("transpilation-worker", "Alexandria.Workers.MediaTranspilation"),
        ("playlist-worker", "Alexandria.Workers.Playlist"),
        ("lyrics-worker", "Alexandria.Workers.Lyrics"),
    ];

    private readonly CachedInfrastructureFixture _infrastructure = new();
    private readonly ProcessRunner _runner = new();
    private readonly CancellationToken _ct = TestContext.Current.CancellationToken;
    private bool _mediaPreviews;
    private readonly bool _lyrics;
    private readonly Action<InstallationContext>? _configure;
    private bool _audioAnalysis;
    private bool _lyricsProviderUnavailable;

    public CachedWorkerProcessingFixture(bool lyrics = false, Action<InstallationContext>? configure = null)
    {
        _lyrics = lyrics;
        _configure = configure;
    }

    private IEnumerable<(string Service, string Project)> Applications =>
        AllApplications.Where(a => _lyrics || a.Service != "lyrics-worker");

    public string OwnerId { get; private set; } = string.Empty;
    public InstallationContext Context => _infrastructure.Context;
    public string Target => _infrastructure.Target;
    public SourceImageFixture? SourceImages => _infrastructure.SourceImages;

    public void PrepareAndStart()
    {
        var context = _infrastructure.Context;
        context.Config.Features = new FeatureSelection
        {
            Enabled = [FeatureCatalog.DocumentPreviews.Id, FeatureCatalog.AdaptiveStreaming.Id]
        };
        if (_lyrics) context.Config.Features.Enabled.Add(FeatureCatalog.Lyrics.Id);
        _configure?.Invoke(context);
        _audioAnalysis = context.Config.Features.AudioBackbone != "off" &&
            context.Config.Features.IsEnabled(FeatureCatalog.Autotagging.Id);
        InstallationConfigurationResolver.Resolve(context, new CredentialService());

        foreach (var (service, project) in Applications)
        {
            if (SourceImages is not null) continue;

            var output = Output(project);
            Require(File.Exists(Path.Combine(output, project + ".dll")) &&
                    File.Exists(Path.Combine(output, project + ".runtimeconfig.json")),
                $"Current compiled output is missing for {project}. Build Backend with cached packages before opting in.");
            RequireSuccess(Docker(["image", "inspect", "--format", "{{.Id}}", Image(service)]),
                $"inspect cached {service} runtime; this fixture never pulls or builds images");
        }

        SourceImages?.ValidateImages(Applications.Select(application => application.Service), arguments => Docker(arguments));

        _infrastructure.Prepare();
        _infrastructure.Start();
        _infrastructure.ReadHandoff();
        _infrastructure.CommitHandoff();
        WriteOverride();
        ValidateRuntimePlan();

        RequireSuccess(Compose(["up", "--detach", "--pull", "never", "--no-build", "--no-deps", "api"]),
            "start current API for migrations and bootstrap");
        WaitUntil(() => Compose(["exec", "--no-TTY", "api", "wget", "-qO-", "http://127.0.0.1:8080/health/ready"]).Succeeded,
            "API migrations/bootstrap and essential readiness", TimeSpan.FromMinutes(2));
        OwnerId = Sql("SELECT \"Id\" FROM \"AspNetUsers\" WHERE \"UserName\"='fixtureadmin';").Trim();
        Require(Guid.TryParse(OwnerId, out _), "The current API did not create the fixture administrator.");

        RequireSuccess(Compose(["up", "--detach", "--pull", "never", "--no-build", "--no-deps",
            .. Applications.Where(a => a.Service != "api").Select(a => a.Service)]), "start current processing workers");

        foreach (var (service, _) in Applications.Where(a => a.Service != "api"))
            WaitUntil(() => Compose(["exec", "--no-TTY", "api", "wget", "-qO-", $"http://{service}:8080/health"]).Succeeded,
                service + " health", TimeSpan.FromMinutes(1));

        foreach (var (service, _) in Applications)
        {
            var container = Compose(["ps", "--quiet", service]);
            RequireSuccess(container, "find the owned " + service + " container");
            WaitUntil(() =>
            {
                var health = Docker(["inspect", "--format", "{{.State.Health.Status}}", container.StandardOutput.Trim()]);
                return health.Succeeded && health.StandardOutput.Trim() == "healthy";
            }, service + " Docker healthcheck", TimeSpan.FromMinutes(1));
        }

        var published = Compose(["ps", "--all", "--format", "json"]);
        RequireSuccess(published, "inspect isolated runtime publications");
        Require(!published.StandardOutput.Contains("HostPort", StringComparison.Ordinal),
            "The processing fixture must not publish a host port.");
    }

    public (string FileId, string VersionId) SeedFile(string name, string mimeType, byte[] bytes)
    {
        var fileId = Guid.NewGuid().ToString();
        var versionId = Guid.NewGuid().ToString();
        var contentId = Guid.NewGuid().ToString();
        var hash = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
        var key = "content/" + hash;
        var source = Path.Combine(_infrastructure.Target, name);
        File.WriteAllBytes(source, bytes);
        var uploadScript = """
            set -eu
            aws --endpoint-url http://garage:3900 s3api put-object --bucket alexandria-files --key "$1" --body "$2" >/dev/null
            """;
        RequireSuccess(Compose(["run", "--rm", "--pull", "never", "--no-deps", "--entrypoint", "/bin/sh", "garage-init",
            "-c", uploadScript, "fixture-upload", key, "/fixture-input/" + name]), "store synthetic input in the private Garage");

        Sql($$"""
            BEGIN;
            INSERT INTO "ContentObjects" ("Id","Hash","StorageKey","IsPromoted","PromotedAt","PromotionAttempts","CreatedAt")
            VALUES ('{{contentId}}',decode('{{hash}}','hex'),'{{key}}',true,NOW(),0,NOW());
            INSERT INTO "Files" ("Id","Name","MimeType","OwnerId","CreatedAt","CurrentVersionId")
            VALUES ('{{fileId}}','{{name}}','{{mimeType}}','{{OwnerId}}',NOW(),NULL);
            INSERT INTO "FileVersions" ("Id","FileId","ContentObjectId","ContentHash","Size","VersionNumber","MimeType","IsEncrypted","CreatedAt","CreatedBy")
            VALUES ('{{versionId}}','{{fileId}}','{{contentId}}',decode('{{hash}}','hex'),{{bytes.Length}},1,'{{mimeType}}',false,NOW(),'{{OwnerId}}');
            UPDATE "Files" SET "CurrentVersionId"='{{versionId}}' WHERE "Id"='{{fileId}}';
            COMMIT;
            """);

        return (fileId, versionId);
    }

    public string QueuePreview(string versionId, string routingKey, bool document = false)
    {
        var jobId = Guid.NewGuid().ToString();
        Sql($$"""
            BEGIN;
            INSERT INTO "Jobs" ("Id","Status","Type","UserId") VALUES ('{{jobId}}','Queued',{{(document ? 1 : 2)}},'{{OwnerId}}');
            INSERT INTO "PreviewJobs" ("Id","JobId","VersionId","Kind","UserId","CreatedAt")
            VALUES ('{{Guid.NewGuid()}}','{{jobId}}','{{versionId}}','Preview','{{OwnerId}}',NOW())
            ON CONFLICT ("VersionId","Kind","UserId") DO UPDATE SET "JobId"=EXCLUDED."JobId";
            COMMIT;
            """);
        Publish(routingKey, jobId);
        return jobId;
    }

    public string QueueTranspilation(string versionId)
    {
        var jobId = Guid.NewGuid().ToString();
        Sql($$"""
            BEGIN;
            INSERT INTO "Jobs" ("Id","Status","Type","UserId") VALUES ('{{jobId}}','Queued',0,'{{OwnerId}}');
            INSERT INTO "TranspilationJobs" ("Id","JobId","VersionId","IsVideo","AudioRungs","VideoRungs","UserId")
            VALUES ('{{Guid.NewGuid()}}','{{jobId}}','{{versionId}}',false,ARRAY[0],ARRAY[]::integer[],'{{OwnerId}}');
            COMMIT;
            """);
        Publish("transpilation.job", jobId);
        return jobId;
    }

    public string QueueLyricsLookup(string transcodeJobId)
    {
        Require(_lyrics, "The lyrics runtime must be selected before publishing a lookup.");
        var jobId = Guid.NewGuid().ToString();
        var transpilationId = Sql($"SELECT \"Id\" FROM \"TranspilationJobs\" WHERE \"JobId\"='{transcodeJobId}';");
        Sql($$"""
            BEGIN;
            INSERT INTO "Jobs" ("Id","Status","Type","UserId") VALUES ('{{jobId}}','Queued',3,'{{OwnerId}}');
            INSERT INTO "TrackLyrics" ("Id","JobId","TranspilationJobId","Status","SourceProvider","Cached","IsInstrumental")
            VALUES ('{{Guid.NewGuid()}}','{{jobId}}','{{transpilationId}}',0,1,false,false);
            COMMIT;
            """);
        Publish("lyrics.job", jobId);
        return jobId;
    }

    public void VerifyLyricsAcknowledged()
    {
        var channels = Compose(["exec", "--no-TTY", "rabbitmq", "rabbitmqctl", "list_channels",
            "user", "consumer_count", "--formatter", "json"]);
        RequireSuccess(channels, "inspect the installed lyrics consumer identity");
        using var channelRows = JsonDocument.Parse(channels.StandardOutput);
        Require(channelRows.RootElement.EnumerateArray().Any(channel =>
                channel.GetProperty("user").GetString() == "lyrics_worker_user" &&
                channel.GetProperty("consumer_count").GetInt32() > 0),
            "The lookup must use the installed lyrics_worker_user consumer.");

        WaitUntil(() =>
        {
            var result = Compose(["exec", "--no-TTY", "rabbitmq", "rabbitmqctl", "list_queues", "--vhost",
                _infrastructure.Context.Config.EnvironmentValues["RABBITMQ_VHOST"], "name", "consumers", "messages_ready", "messages_unacknowledged",
                "--formatter", "json"]);
            RequireSuccess(result, "inspect the real lyrics queue acknowledgement");
            using var queues = JsonDocument.Parse(result.StandardOutput);
            var queue = queues.RootElement.EnumerateArray().Single(q => q.GetProperty("name").GetString() == "lyrics-queue");
            return queue.GetProperty("consumers").GetInt32() == 1 &&
                queue.GetProperty("messages_ready").GetInt32() == 0 &&
                queue.GetProperty("messages_unacknowledged").GetInt32() == 0;
        }, "lyrics lookup acknowledgement", TimeSpan.FromSeconds(20));
    }

    public void SetLyricsProviderAvailability(bool available)
    {
        Require(_lyrics, "The lyrics runtime must be selected before changing its fixture provider connection.");
        _lyricsProviderUnavailable = !available;
        WriteOverride();
        RequireSuccess(Compose(["up", "--detach", "--force-recreate", "--pull", "never", "--no-build", "--no-deps", "lyrics-worker"]),
            "restart the owned lyrics worker with the fixture provider connection");
        WaitUntil(() => Compose(["exec", "--no-TTY", "api", "wget", "-qO-", "http://lyrics-worker:8080/health"]).Succeeded,
            "lyrics worker after changing the fixture provider connection", TimeSpan.FromMinutes(1));
    }

    public void WaitForLyricsProviderRetry(string jobId)
    {
        WaitUntil(() => Sql($"SELECT COUNT(*) FROM \"Jobs\" WHERE \"Id\"='{jobId}' " +
                "AND \"Status\"='Failed' AND \"RetryCount\">=1;") == "1",
            "a real counted lyrics retry after provider failure", TimeSpan.FromSeconds(40));
        Require(Sql($"SELECT \"Status\" FROM \"TrackLyrics\" WHERE \"JobId\"='{jobId}';") == "0",
            "A provider outage must remain pending rather than record NoMatch.");
    }

    public void WaitForLyricsNoMatch(string jobId)
    {
        WaitUntil(() => Sql($"SELECT COUNT(*) FROM \"TrackLyrics\" l JOIN \"Jobs\" j ON j.\"Id\"=l.\"JobId\" " +
                $"WHERE j.\"Id\"='{jobId}' AND j.\"Status\"='Ready' AND l.\"Status\"=4;") == "1",
            "the persisted lyrics provider no-match result", TimeSpan.FromMinutes(2));
    }

    public void EnableMediaPreviews()
    {
        _mediaPreviews = true;
        WriteOverride();
        RequireSuccess(Compose(["up", "--detach", "--force-recreate", "--pull", "never", "--no-build", "--no-deps", "media-worker"]),
            "restart the owned media worker with previews enabled");
        WaitUntil(() => Compose(["exec", "--no-TTY", "api", "wget", "-qO-", "http://media-worker:8080/health"]).Succeeded,
            "media worker after enabling previews", TimeSpan.FromMinutes(1));
    }

    public void WaitForJob(string jobId)
    {
        WaitUntil(() =>
        {
            var status = Sql($"SELECT \"Status\" FROM \"Jobs\" WHERE \"Id\"='{jobId}';").Trim();
            if (status is "Failed" or "Cancelled")
            {
                var detail = Sql($"SELECT COALESCE(\"ErrorDetail\",'') FROM \"Jobs\" WHERE \"Id\"='{jobId}';");
                var logs = Compose(["logs", "--no-color", "--tail", "25", .. Applications.Select(a => a.Service)]);
                throw new InvalidOperationException("A real processing job terminated unsuccessfully: " + detail + "\n" +
                    Redact(logs.StandardOutput + logs.StandardError));
            }
            return status == "Ready";
        }, "durable processing job completion", TimeSpan.FromMinutes(2));
    }

    public void RequestPlaylistSync(string fileId)
    {
        Publish("playlist.sync", JsonSerializer.Serialize(new Dictionary<string, string>
        {
            ["FileId"] = fileId,
            ["OwnerId"] = OwnerId
        }, new JsonSerializerOptions()));
    }

    public void WaitForArtistPlaylist(string artist)
    {
        WaitUntil(() => Sql($"SELECT COUNT(*) FROM \"Playlists\" WHERE \"OwnerId\"='{OwnerId}' " +
                $"AND \"IsAutoGenerated\" AND \"Name\"='{artist}' AND \"DeletedAt\" IS NULL;").Trim() == "1",
            "playlist notification synchronization", TimeSpan.FromMinutes(1));
    }

    public void VerifyPeriodicPlaylistSweep()
    {
        var runs = int.Parse(Sql("SELECT COUNT(*) FROM \"Jobs\" WHERE \"Type\"=5 AND \"Status\"='Ready';"), CultureInfo.InvariantCulture);
        WaitUntil(() => int.Parse(Sql("SELECT COUNT(*) FROM \"Jobs\" WHERE \"Type\"=5 AND \"Status\"='Ready';"), CultureInfo.InvariantCulture) > runs,
            "periodic playlist sweep", TimeSpan.FromSeconds(35));
    }

    public string Sql(string sql)
    {
        var result = Compose(["exec", "--no-TTY", "postgres", "/bin/sh", "-c", """
            export PGPASSWORD="$POSTGRES_PASSWORD"
            exec psql --host 127.0.0.1 --username "$POSTGRES_USER" --dbname "$POSTGRES_DB" --no-password --no-psqlrc --set ON_ERROR_STOP=1 --tuples-only --no-align --command "$1"
            """, "fixture-sql", sql]);
        RequireSuccess(result, "query the owned fixture database");
        return result.StandardOutput.Trim();
    }

    public void VerifyStoredOutputs(string bucket, string prefix)
    {
        var script = """
            set -eu
            aws --endpoint-url http://garage:3900 s3api list-objects-v2 --bucket "$1" --prefix "$2" --query 'length(Contents)' --output text
            """;
        var result = Compose(["run", "--rm", "--pull", "never", "--no-deps", "--entrypoint", "/bin/sh", "garage-init",
            "-c", script, "fixture-storage", bucket, prefix]);
        RequireSuccess(result, "verify generated objects exist in private storage");
        Require(int.TryParse(result.StandardOutput.Trim(), out var count) && count > 0,
            "A completed job has no corresponding stored output.");
    }

    public void Publish(string routingKey, string payload)
    {
        var values = _infrastructure.Context.Config.EnvironmentValues;
        var vhost = Uri.EscapeDataString(values["RABBITMQ_VHOST"]);
        var message = JsonSerializer.Serialize(new
        {
            properties = new { delivery_mode = 2 }, routing_key = routingKey, payload, payload_encoding = "string"
        }, new JsonSerializerOptions());
        var result = Compose(["exec", "--no-TTY", "api", "wget", "-qO-", "--header", "Content-Type: application/json",
            "--header", "Authorization: Basic " + Convert.ToBase64String(Encoding.UTF8.GetBytes(
                values["RABBITMQ_USER"] + ":" + values["RABBITMQ_PASSWORD"])), "--post-data", message,
            $"http://rabbitmq:15672/api/exchanges/{vhost}/content-exchange/publish"]);
        RequireSuccess(result, "publish a durable job through the real broker");
        using var response = JsonDocument.Parse(result.StandardOutput);
        Require(response.RootElement.GetProperty("routed").GetBoolean(), "The real broker did not route the fixture job to its consumer queue.");
    }

    private void WriteOverride()
    {
        if (SourceImages is not null)
        {
            WriteSourceOverride();
            return;
        }

        var source = _infrastructure.Context.SourceRoot;
        var settings = Path.Combine(_infrastructure.Target, "fixture-empty-settings.json");
        ProtectedFile.WriteAtomic(settings, "{}");
        if (!OperatingSystem.IsWindows())
            File.SetUnixFileMode(settings, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.GroupRead | UnixFileMode.OtherRead);

        var text = new StringBuilder($$"""
            services:
              garage-init:
                build: !reset null
                image: alexandria-local-garage-init:latest
                pull_policy: never
                environment:
                  AWS_DEFAULT_REGION: garage
                  AWS_ACCESS_KEY_ID: ${GARAGE_S3_ACCESS_KEY:?GARAGE_S3_ACCESS_KEY is required}
                  AWS_SECRET_ACCESS_KEY: ${GARAGE_S3_SECRET_KEY:?GARAGE_S3_SECRET_KEY is required}
                volumes:
                  - {{Yaml(Path.Combine(source, "scripts", "garage-init", "init.sh") + ":/scripts/init.sh:ro")}}
                  - {{Yaml(_infrastructure.Target + ":/fixture-input:ro")}}

            """);

        foreach (var (service, project) in Applications)
        {
            text.AppendLine($$"""
              {{service}}:
                build: !reset null
                image: {{Image(service)}}
                pull_policy: never
                entrypoint: ["dotnet", "{{project}}.dll"]
                environment:
                  ASPNETCORE_CONTENTROOT: /fixture-config
                  RabbitMQ__HostName: rabbitmq
                  RabbitMQ__Consumer__SweepIntervalSeconds: "10"
                  Features__MediaPreviews: "{{_mediaPreviews.ToString().ToLowerInvariant()}}"
                  Features__AudioAnalysis: "{{_audioAnalysis.ToString().ToLowerInvariant()}}"
                  Features__Autotagging: "{{_audioAnalysis.ToString().ToLowerInvariant()}}"
            """);
            if (service == "lyrics-worker")
            {
                var proxy = _lyricsProviderUnavailable ? "http://127.0.0.1:1" : string.Empty;
                text.AppendLine($"      HTTPS_PROXY: \"{proxy}\"");
            }

            text.AppendLine($$"""
                volumes:
                  - {{Yaml(Output(project) + ":/app:ro")}}
                  - {{Yaml(settings + ":/fixture-config/appsettings.json:ro")}}
            """);
        }

        ProtectedFile.WriteAtomic(Path.Combine(_infrastructure.Target, "docker-compose.fixture.yml"), text.ToString());
    }

    private void WriteSourceOverride()
    {
        var text = new StringBuilder("services:\n");
        SourceImages!.AppendOverride(text, "garage-init");
        text.AppendLine($$"""
                environment:
                  AWS_DEFAULT_REGION: garage
                  AWS_ACCESS_KEY_ID: ${GARAGE_S3_ACCESS_KEY:?GARAGE_S3_ACCESS_KEY is required}
                  AWS_SECRET_ACCESS_KEY: ${GARAGE_S3_SECRET_KEY:?GARAGE_S3_SECRET_KEY is required}
                volumes:
                  - {{Yaml(_infrastructure.Target + ":/fixture-input:ro")}}
            """);

        foreach (var (service, _) in Applications)
        {
            SourceImages.AppendOverride(text, service);
            text.AppendLine($$"""
                environment:
                  RabbitMQ__HostName: rabbitmq
                  RabbitMQ__Consumer__SweepIntervalSeconds: "10"
                  Features__MediaPreviews: "{{_mediaPreviews.ToString().ToLowerInvariant()}}"
                  Features__AudioAnalysis: "{{_audioAnalysis.ToString().ToLowerInvariant()}}"
                  Features__Autotagging: "{{_audioAnalysis.ToString().ToLowerInvariant()}}"
            """);

            if (service == "lyrics-worker")
            {
                var proxy = _lyricsProviderUnavailable ? "http://127.0.0.1:1" : string.Empty;
                text.AppendLine($"      HTTPS_PROXY: \"{proxy}\"");
            }
        }

        ProtectedFile.WriteAtomic(Path.Combine(_infrastructure.Target, "docker-compose.fixture.yml"), text.ToString());
    }

    private void ValidateRuntimePlan()
    {
        var result = Compose(["config", "--format", "json"]);
        RequireSuccess(result, "validate the isolated processing composition");
        using var parsed = JsonDocument.Parse(result.StandardOutput);
        var services = parsed.RootElement.GetProperty("services");
        SourceImages?.ValidateCompose(services, Applications.Select(application => application.Service).Append("garage-init"));
        foreach (var (service, _) in Applications)
        {
            Require(services.TryGetProperty(service, out var definition), "The generated installation omits " + service);
            Require(!definition.TryGetProperty("build", out _), "A cached processing fixture cannot contain active application builds.");
            Require(!definition.TryGetProperty("ports", out var ports) || ports.GetArrayLength() == 0,
                "The processing fixture cannot publish application host ports.");
        }
    }

    private void WaitUntil(Func<bool> predicate, string operation, TimeSpan timeout)
    {
        var timer = Stopwatch.StartNew();
        while (timer.Elapsed < timeout)
        {
            _ct.ThrowIfCancellationRequested();
            if (predicate()) return;
            Task.Delay(TimeSpan.FromMilliseconds(500), _ct).GetAwaiter().GetResult();
        }

        var logs = Compose(["logs", "--no-color", "--tail", "30", .. Applications.Select(a => a.Service)]);
        throw new InvalidOperationException("Timed out waiting for " + operation + ". " + Redact(logs.StandardOutput + logs.StandardError));
    }

    private string Output(string project) => Path.Combine(_infrastructure.Context.SourceRoot, "Backend", project, "bin", "Debug", "net10.0");

    private static string Image(string service) => "alexandria-local-" + service + ":latest";

    public ProcessResult Compose(IReadOnlyList<string> arguments) => _infrastructure.Compose(arguments, TimeSpan.FromSeconds(30));

    private ProcessResult Docker(IReadOnlyList<string> arguments)
    {
        var start = new ProcessStartInfo("docker") { RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false };
        foreach (var argument in arguments) start.ArgumentList.Add(argument);
        return _runner.Run(start, TimeSpan.FromSeconds(30), _ct);
    }

    private string Redact(string value) => new SecretRedactor(() => _infrastructure.Context.Config.EnvironmentValues).Redact(value);

    private void RequireSuccess(ProcessResult result, string operation) => Require(result.Succeeded,
        "Could not " + operation + ": " + Redact(result.StandardError));

    private static void Require(bool condition, string message) => CachedInfrastructureFixture.Require(condition, message);

    private static string Yaml(string value) => "'" + value.Replace("$", "$$", StringComparison.Ordinal).Replace("'", "''", StringComparison.Ordinal) + "'";

    public void Dispose() => _infrastructure.Dispose();
}
