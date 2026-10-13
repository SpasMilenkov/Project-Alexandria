using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Builder.Models;
using Builder.Services;
using Builder.Workflow;

namespace Builder.Tests.Integration;

public sealed class RecoveryRuntimeTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly CancellationToken _ct = TestContext.Current.CancellationToken;

    [DockerFact]
    [Trait("Category", "Docker")]
    public async Task Repair_after_restart_and_stopped_worker_preserves_uploaded_data_credentials_and_unrelated_installation()
    {
        using var fixture = new CachedHttpsFixture();
        using var unrelated = new CachedInfrastructureFixture();
        fixture.Prepare();
        unrelated.Prepare();
        unrelated.Start();
        fixture.Start();

        var configuration = new ConfigurationService(new TemplateService("Local"));
        fixture.Context.Config.Stage = "complete";
        configuration.SaveConfiguration(fixture.Context.Config, fixture.Target);
        var docker = new CachedRecoveryDockerService(fixture);
        docker.Configure(fixture.Context.Config, _ct);
        var redactor = new SecretRedactor(() => fixture.Context.Config.EnvironmentValues);
        var evidence = new List<string>();
        void Capture(string line) => evidence.Add(redactor.Redact(line));

        try
        {
            using var client = fixture.CreateClient();
            var profile = await Login(fixture, client);
            var uploaded = await UploadEncryptedFile(fixture, client);
            await VerifyEncryptedFile(fixture, client, uploaded);
            fixture.ValidateCoreHealth();

            var artifacts = SnapshotArtifacts(fixture.Target);
            var metadata = MetadataHash(fixture.Target);
            var ownedBefore = ObserveContainers(docker, fixture.Context.Config.ProjectName);
            var unrelatedBefore = ObserveContainers(docker, unrelated.Context.Config.ProjectName);
            var capabilities = ServiceCatalog.Resolve(fixture.Context.Features);
            var persistentServices = capabilities.ServiceIds.Except(capabilities.OneShotServiceIds, StringComparer.Ordinal).ToArray();
            fixture.RequireSuccess(fixture.Compose(["restart", "--timeout", "10", .. persistentServices], TimeSpan.FromMinutes(3)),
                "restart the disposable installation");
            RequireReady(docker, fixture, capabilities, Capture);

            var restarted = ObserveContainers(docker, fixture.Context.Config.ProjectName);
            RequireRestarted(ownedBefore, restarted, persistentServices);
            RequireSameContainers(unrelatedBefore, ObserveContainers(docker, unrelated.Context.Config.ProjectName));
            RequireSameArtifacts(artifacts, SnapshotArtifacts(fixture.Target));
            Require(MetadataHash(fixture.Target) == metadata, "Restart changed persisted installation metadata.");
            await VerifyProfile(client, profile);
            await VerifyEncryptedFile(fixture, client, uploaded);

            fixture.RequireSuccess(fixture.Compose(["stop", "--timeout", "10", "media-worker"]), "stop the owned media worker");
            var stopped = docker.GetServiceStatuses(fixture.Target, Capture).Single(status => status.Service == "media-worker");
            Require(stopped.State == "exited", "The intentional worker failure was not visible in real Docker status.");
            Require(!docker.WaitForServices(fixture.Target, capabilities.ServiceIds, TimeSpan.FromSeconds(15),
                    capabilities.OneShotServiceIds, onOutput: Capture),
                "Readiness reported success while a selected worker was stopped.");
            Require(evidence.Any(line => line.Contains("media-worker", StringComparison.Ordinal) && line.Contains("exited", StringComparison.Ordinal)),
                "Readiness did not identify the stopped worker in its diagnostic evidence.");

            var saved = configuration.LoadConfiguration(fixture.Target);
            Require(saved is not null, "Repair could not reload installation metadata.");
            var repair = new InstallationContext { Config = saved!, IsRepair = true };

            try
            {
                InstallationConfigurationResolver.Resolve(repair, new CredentialService());
                repair.ApprovedEnvironmentContent = File.ReadAllText(Path.Combine(fixture.Target, ".env"));
                using var loopback = fixture.CreateHandler(authenticatedCookies: false);
                HttpMessageHandler CreateHandler(InstallationConfig config, CookieContainer cookies)
                {
                    var handler = HttpsEndpointVerifier.CreateHandler(config, cookies);
                    handler.ConnectCallback = loopback.ConnectCallback;

                    return handler;
                }

                var stages = new List<string>();
                var orchestrator = new InstallationOrchestrator(configuration, docker,
                    httpsVerifier: new HttpsEndpointVerifier(CreateHandler));
                orchestrator.Execute(repair, (stage, action) =>
                {
                    stages.Add(stage);
                    Capture("Stage: " + stage);
                    action(Capture);
                });
                Require(stages.Contains("Checking every selected service") && stages.Contains("Checking the HTTPS address"),
                    "Repair did not finish service and external HTTPS verification.");
                Require(docker.CachedImageChecks == 1, "Repair did not validate its cached images exactly once.");
                Require(repair.DeploymentVerified && repair.Config.Stage == "complete" && !repair.AdminLoginVerified,
                    "Repair must commit completion after its checks and retain the explicit limit on administrator verification.");
                Require(!InstallationConfigurationResolver.Inspect(repair).IsIncompleteGeneration,
                    "Repair left an inconsistent generation checkpoint.");
            }
            finally
            {
                repair.Cancellation.Dispose();
            }

            RequireReady(docker, fixture, capabilities, Capture);
            fixture.ValidateCoreHealth();
            RequireSameArtifacts(artifacts, SnapshotArtifacts(fixture.Target));
            Require(MetadataHash(fixture.Target) == metadata, "Repair changed installation identity, capabilities, storage, or certificate settings.");
            RequireSameVolumes(ownedBefore, ObserveContainers(docker, fixture.Context.Config.ProjectName));
            RequireSameContainers(unrelatedBefore, ObserveContainers(docker, unrelated.Context.Config.ProjectName));
            await VerifyProfile(client, profile);
            await VerifyEncryptedFile(fixture, client, uploaded);
            Require(await Login(fixture, client) == profile, "Repair did not preserve the original admin account and password.");
            TestContext.Current.TestOutputHelper!.WriteLine(
                "Real restart and stopped-worker repair passed using generated settings, cached images, current mounted outputs, and production orchestration/readiness/ownership/HTTPS checks. " +
                "The encrypted API upload, admin login, credentials, certificates, named data attachments, and separate fixture's container start times were retained. Source image builds remain a separate gate.");
        }
        catch
        {
            var statuses = fixture.Compose(["ps", "--all", "--format", "json"]);
            var logs = fixture.Compose(["logs", "--no-color", "--tail", "25"]);
            Capture(statuses.StandardOutput + statuses.StandardError);
            Capture(logs.StandardOutput + logs.StandardError);
            TestContext.Current.TestOutputHelper!.WriteLine(string.Join(Environment.NewLine, evidence));

            throw;
        }
    }

    private async Task<string> Login(CachedHttpsFixture fixture, HttpClient client)
    {
        using var response = await client.PostAsJsonAsync("/api/auth/login", new
        {
            email = fixture.Context.AdminAccount.Email,
            password = fixture.Context.AdminAccount.Password,
        }, JsonOptions, _ct);
        RequireStatus(response, "admin login");
        using var identity = await ReadJson(response);
        Require(identity.RootElement.GetProperty("success").GetBoolean() &&
                identity.RootElement.GetProperty("userRoles").EnumerateArray().Any(role => role.GetString() == "Admin"),
            "The original account no longer has administrator access.");
        using var profile = await client.GetAsync("/api/users/profile", _ct);
        RequireStatus(profile, "authenticated profile");

        return Convert.ToHexString(SHA256.HashData(await profile.Content.ReadAsByteArrayAsync(_ct)));
    }

    private async Task VerifyProfile(HttpClient client, string expected)
    {
        using var profile = await client.GetAsync("/api/users/profile", _ct);
        RequireStatus(profile, "retained authenticated session");
        Require(Convert.ToHexString(SHA256.HashData(await profile.Content.ReadAsByteArrayAsync(_ct))) == expected,
            "The existing authenticated session no longer resolves to the original account.");
    }

    private async Task<EncryptedFile> UploadEncryptedFile(CachedHttpsFixture fixture, HttpClient client)
    {
        const string password = "synthetic recovery file password";
        const int iterations = 800_000;
        var plaintext = Encoding.UTF8.GetBytes("Recovery retains encrypted data: résumé +% #?& 日本語\n");
        var salt = RandomNumberGenerator.GetBytes(16);
        var iv = RandomNumberGenerator.GetBytes(12);
        var tag = new byte[16];
        var ciphertext = new byte[plaintext.Length];
        var key = Rfc2898DeriveBytes.Pbkdf2(password, salt, iterations, HashAlgorithmName.SHA256, 32);

        using (var aes = new AesGcm(key, tag.Length)) aes.Encrypt(iv, plaintext, ciphertext, tag);

        using var initialized = await client.PostAsJsonAsync("/api/files/init-upload", new
        {
            contentType = "application/octet-stream",
            hash = fixture.ContentHash(ciphertext),
            contentLength = ciphertext.Length,
        }, JsonOptions, _ct);
        RequireStatus(initialized, "encrypted upload initialization");
        using var upload = await ReadJson(initialized);
        var uploadUrl = upload.RootElement.GetProperty("uploadUrl").GetString()!;
        RequirePublicOrigin(fixture, uploadUrl);
        using var content = new ByteArrayContent(ciphertext);
        content.Headers.ContentType = new("application/octet-stream");
        using var uploaded = await client.PutAsync(uploadUrl, content, _ct);
        RequireStatus(uploaded, "presigned encrypted upload");

        var fileName = "recovery résumé " + Guid.NewGuid().ToString("N") + ".bin";
        using var finalized = await client.PostAsJsonAsync("/api/files/finalize-upload", new
        {
            uploadId = upload.RootElement.GetProperty("uploadId").GetGuid(),
            fileName,
            isEncrypted = true,
            encryptionIv = iv,
            encryptionSalt = salt,
            integrityTag = tag,
            iterationCount = iterations,
        }, JsonOptions, _ct);
        RequireStatus(finalized, "encrypted upload finalization");
        using var searched = await client.GetAsync("/api/files/search?currentPage=0&pageSize=20&nameContains=" + Uri.EscapeDataString(fileName), _ct);
        RequireStatus(searched, "uploaded file lookup");
        using var files = await ReadJson(searched);
        var items = files.RootElement.GetProperty("items").EnumerateArray().ToArray();
        Require(items.Length == 1, "The encrypted upload did not persist a unique file row.");

        return new EncryptedFile(items[0].GetProperty("fileId").GetGuid(), plaintext, ciphertext, iv, salt, tag, password, iterations);
    }

    private async Task VerifyEncryptedFile(CachedHttpsFixture fixture, HttpClient client, EncryptedFile file)
    {
        using var downloadInfo = await client.GetAsync("/api/files/download/" + file.Id, _ct);
        RequireStatus(downloadInfo, "retained file download initialization");
        using var document = await ReadJson(downloadInfo);
        var details = document.RootElement;
        var url = details.GetProperty("presignedUrl").GetString()!;
        RequirePublicOrigin(fixture, url);
        Require(details.GetProperty("isEncrypted").GetBoolean() && details.GetProperty("iterationCount").GetInt32() == file.Iterations &&
                details.GetProperty("encryptionIv").GetBytesFromBase64().AsSpan().SequenceEqual(file.Iv) &&
                details.GetProperty("encryptionSalt").GetBytesFromBase64().AsSpan().SequenceEqual(file.Salt) &&
                details.GetProperty("integrityTag").GetBytesFromBase64().AsSpan().SequenceEqual(file.Tag),
            "The database did not preserve the encrypted file's metadata.");
        using var downloaded = await client.GetAsync(url, _ct);
        RequireStatus(downloaded, "retained presigned file download");
        var bytes = await downloaded.Content.ReadAsByteArrayAsync(_ct);
        Require(bytes.AsSpan().SequenceEqual(file.Ciphertext), "Stored encrypted bytes changed during restart or repair.");
        var key = Rfc2898DeriveBytes.Pbkdf2(file.Password, file.Salt, file.Iterations, HashAlgorithmName.SHA256, 32);
        var plaintext = new byte[bytes.Length];

        using (var aes = new AesGcm(key, file.Tag.Length)) aes.Decrypt(file.Iv, bytes, file.Tag, plaintext);

        Require(plaintext.AsSpan().SequenceEqual(file.Plaintext), "The retained encrypted file could not be decrypted to its original content.");
    }

    private static Dictionary<string, string> SnapshotArtifacts(string target)
    {
        var checkpoint = JsonSerializer.Deserialize(File.ReadAllText(Path.Combine(target, InstallationConfigurationResolver.GenerationMarkerName)),
            AlexandriaJsonContext.Default.GenerationCheckpoint)!;

        return checkpoint.ArtifactHashes.Keys.Append("init-output/garage-capacity.bytes").Append("docker-compose.fixture.yml")
            .Distinct(StringComparer.Ordinal).ToDictionary(path => path,
                path => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(Path.Combine(target, path)))), StringComparer.Ordinal);
    }

    private static void RequireSameArtifacts(Dictionary<string, string> expected, Dictionary<string, string> actual)
    {
        Require(expected.Count == actual.Count && expected.All(pair => actual.GetValueOrDefault(pair.Key) == pair.Value),
            "Restart or repair changed established artifacts: " + string.Join(", ", expected.Keys.Union(actual.Keys)
                .Where(path => expected.GetValueOrDefault(path) != actual.GetValueOrDefault(path))) + ".");
    }

    private static string MetadataHash(string target)
    {
        var metadata = JsonNode.Parse(File.ReadAllText(Path.Combine(target, "alexandria-config.json")))!.AsObject();
        metadata.Remove("Stage");

        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(metadata.ToJsonString(JsonOptions))));
    }

    private static Dictionary<string, ObservedContainer> ObserveContainers(CachedRecoveryDockerService docker, string project)
    {
        var listed = docker.QueryDocker(["ps", "--all", "--quiet", "--filter", "label=com.docker.compose.project=" + project]);
        Require(listed.Succeeded, "Could not observe disposable container identities.");
        var ids = listed.StandardOutput.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        Require(ids.Length > 0, "The observed disposable installation has no containers.");
        const string format = "{\"Id\":{{json .Id}},\"Labels\":{{json .Config.Labels}},\"State\":{{json .State.Status}},\"StartedAt\":{{json .State.StartedAt}},\"RestartCount\":{{json .RestartCount}},\"Mounts\":{{json .Mounts}}}";
        var inspected = docker.QueryDocker(["inspect", "--format", format, .. ids]);
        Require(inspected.Succeeded, "Could not inspect disposable container start times and data attachments.");
        var observed = new Dictionary<string, ObservedContainer>(StringComparer.Ordinal);

        foreach (var line in inspected.StandardOutput.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            using var parsed = JsonDocument.Parse(line);
            var container = parsed.RootElement;
            var labels = container.GetProperty("Labels");
            Require(labels.GetProperty("com.docker.compose.project").GetString() == project,
                "Container inspection escaped the requested disposable project.");
            var service = labels.GetProperty("com.docker.compose.service").GetString()!;
            var volumes = string.Join("\n", container.GetProperty("Mounts").EnumerateArray()
                .Where(mount => mount.GetProperty("Type").GetString() == "volume")
                .Select(mount => mount.GetProperty("Name").GetString() + ":" + mount.GetProperty("Destination").GetString())
                .Order(StringComparer.Ordinal));
            observed.Add(service, new ObservedContainer(container.GetProperty("Id").GetString()!,
                container.GetProperty("State").GetString()!, container.GetProperty("StartedAt").GetString()!,
                container.GetProperty("RestartCount").GetInt32(), volumes));
        }

        return observed;
    }

    private static void RequireRestarted(Dictionary<string, ObservedContainer> before, Dictionary<string, ObservedContainer> after,
        IReadOnlyList<string> services) => Require(services.All(service => before[service].Id == after[service].Id &&
        before[service].StartedAt != after[service].StartedAt), "The restart did not actually restart every persistent owned service.");

    private static void RequireSameContainers(Dictionary<string, ObservedContainer> before, Dictionary<string, ObservedContainer> after) =>
        Require(before.Count == after.Count && before.All(pair => after.GetValueOrDefault(pair.Key) == pair.Value),
            "Repair or restart changed a separate installation's container identity, start time, restart count, state, or data attachment.");

    private static void RequireSameVolumes(Dictionary<string, ObservedContainer> before, Dictionary<string, ObservedContainer> after) =>
        Require(before.Count == after.Count && before.All(pair => after.TryGetValue(pair.Key, out var container) && container.Volumes == pair.Value.Volumes),
            "Repair changed the owned installation's named data-volume attachments.");

    private static void RequireReady(CachedRecoveryDockerService docker, CachedHttpsFixture fixture, ResolvedCapabilities capabilities,
        Action<string> capture) => Require(docker.WaitForServices(fixture.Target, capabilities.ServiceIds, TimeSpan.FromMinutes(3),
        capabilities.OneShotServiceIds, onOutput: capture), "The disposable installation did not regain readiness.");

    private static void RequirePublicOrigin(CachedHttpsFixture fixture, string value) => Require(
        Uri.TryCreate(value, UriKind.Absolute, out var uri) && uri.GetLeftPart(UriPartial.Authority) == fixture.Origin.GetLeftPart(UriPartial.Authority),
        "A presigned recovery URL escaped the selected HTTPS origin.");

    private static void RequireStatus(HttpResponseMessage response, string operation) => Require(response.StatusCode == HttpStatusCode.OK,
        $"Recovery {operation} returned HTTP {(int)response.StatusCode}.");

    private async Task<JsonDocument> ReadJson(HttpResponseMessage response) =>
        JsonDocument.Parse(await response.Content.ReadAsStringAsync(_ct));

    private static void Require(bool condition, string message) => CachedHttpsFixture.Require(condition, message);

    private sealed record EncryptedFile(Guid Id, byte[] Plaintext, byte[] Ciphertext, byte[] Iv, byte[] Salt, byte[] Tag, string Password, int Iterations);

    private sealed record ObservedContainer(string Id, string State, string StartedAt, int RestartCount, string Volumes);
}
