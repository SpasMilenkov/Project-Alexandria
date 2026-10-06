using System.Diagnostics;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Builder.Models;
using Builder.Services;
using Builder.Workflow;

namespace Builder.Tests.Integration;

internal sealed class CachedHttpsFixture : IDisposable
{
    private static readonly string[] SourceServices = ["garage-init", "api", "media-worker", "playlist-worker", "frontend", "nginx"];

    private static readonly string[] Images =
    [
        "postgres:16-alpine", "rabbitmq:4.2.5-management-alpine", "dxflrs/garage:v2.2.0",
        "alpine:3.20", "alexandria-local-garage-init:latest", "alexandria-local-api:latest",
        "alexandria-local-frontend:latest", "alexandria-local-nginx:latest",
        "alexandria-local-media-worker:latest", "alexandria-local-playlist-worker:latest",
    ];

    private readonly ProcessRunner _runner = new();
    private readonly ConfigurationService _configuration = new(new TemplateService("Local"));
    private readonly Dictionary<string, string> _secrets = new(StringComparer.Ordinal);
    private readonly SecretRedactor _redactor;
    private readonly CancellationToken _ct = TestContext.Current.CancellationToken;
    private readonly string _bindAddress;
    private readonly SourceImageFixture? _sourceImages = SourceImageFixture.FromEnvironment();
    private string? _sourceBlake3Output;
    private string? _trustCertificatePath;
    private bool _ownsResources;
    private bool _started;

    public CachedHttpsFixture(bool monitoring = false, PublicEndpoint? lanEndpoint = null, CertificateSettings? certificates = null,
        string? lanBindAddress = null)
    {
        Require(Environment.GetEnvironmentVariable("ALEXANDRIA_BUILDER_DOCKER_TESTS") == "1",
            "Docker integration tests require explicit opt-in.");

        var identity = Guid.NewGuid().ToString("N");
        Target = Path.Combine(Path.GetTempPath(), "alexandria builder https " + identity);
        Context = new InstallationContext
        {
            InstallPath = Target,
            SourceRoot = FindSourceRoot(),
            // Transport fixtures include core metadata and playlists; optional processors are tested separately.
            Features = new FeatureSelection(),
            AdminAccount = new AdminAccountInput
            {
                Username = "transportadmin",
                Email = "transport@example.invalid",
                Password = new CredentialService().GeneratePassword() + "!1aA",
            },
        };
        Context.Config.ProjectName = "builder-https-" + identity;
        Context.Config.PublicEndpoint = lanEndpoint ?? new() { Host = "192.0.2.99", HttpsPort = ReserveAvailablePort() };
        if (lanEndpoint is not null)
        {
            lanEndpoint.Validate();
            lanBindAddress ??= lanEndpoint.Host;
            Require(PublicEndpoint.TryNormalizeHost(lanBindAddress, out var normalized, out _) &&
                    IPAddress.TryParse(normalized, out var address) && address.AddressFamily == AddressFamily.InterNetwork,
                "The manual LAN fixture requires an explicit LAN IPv4 bind address, separate from a DNS public hostname when needed.");
            lanBindAddress = normalized;
        }

        _bindAddress = lanBindAddress ?? "127.0.0.1";

        if (certificates is not null) Context.Config.Certificates = certificates;

        Context.Config.Features.SetEnabled(FeatureCatalog.Monitoring.Id, monitoring);
        InstallationConfigurationResolver.Resolve(Context, new CredentialService());
        _redactor = new SecretRedactor(() => _secrets);
        RememberSecrets();
    }

    public InstallationContext Context { get; }
    public string Target { get; }
    public Uri Origin => new(Context.Config.PublicEndpoint.Origin);
    public CookieContainer Cookies { get; } = new();
    public string ApiOutput => Path.Combine(Context.SourceRoot, "Backend", "Alexandria.Api", "bin", "Debug", "net10.0");
    public string FrontendOutput => Path.Combine(Context.SourceRoot, "Frontend", "dist");

    public void Prepare(bool requireApplication = true, bool importedCertificate = false)
    {
        var images = Images.AsEnumerable();

        if (Context.Config.Features.IsEnabled(FeatureCatalog.Monitoring.Id))
        {
            var template = File.ReadAllText(Path.Combine(Context.SourceRoot, "Builder", "Templates", "Local", "docker-compose", "monitoring.yml.template"));
            images = images.Concat(Regex.Matches(template, @"(?m)^\s+image:\s*(\S+)").Select(match => match.Groups[1].Value));
        }

        foreach (var image in images)
        {
            if (_sourceImages is not null && image.StartsWith("alexandria-local-", StringComparison.Ordinal)) continue;

            var inspected = Docker(["image", "inspect", "--format", "{{.Id}}", image]);
            Require(inspected.Succeeded, $"Required cached image '{image}' is unavailable. This suite never pulls or builds images.");
        }

        _sourceImages?.ValidateImages(SourceServices, arguments => Docker(arguments));

        if (requireApplication && _sourceImages is null)
        {
            Require(File.Exists(Path.Combine(ApiOutput, "Alexandria.Api.dll")) && File.Exists(Path.Combine(ApiOutput, "Blake3.dll")),
                "Current compiled API output is required at Backend/Alexandria.Api/bin/Debug/net10.0. Build it using cached prerequisites before opting in.");
            foreach (var project in new[] { "Alexandria.Workers.Media", "Alexandria.Workers.Playlist" })
            {
                Require(File.Exists(Path.Combine(WorkerOutput(project), project + ".dll")),
                    $"Current compiled {project} output is required. Build using cached prerequisites before opting in.");
            }

            Require(File.Exists(Path.Combine(FrontendOutput, "index.html")),
                "A current frontend production bundle is required at Frontend/dist. Build it using existing dependencies before opting in.");
        }

        foreach (var arguments in new[]
        {
            new[] { "ps", "--all", "--quiet", "--filter", "label=com.docker.compose.project=" + Context.Config.ProjectName },
            new[] { "volume", "ls", "--quiet", "--filter", "label=com.docker.compose.project=" + Context.Config.ProjectName },
            new[] { "network", "ls", "--quiet", "--filter", "label=com.docker.compose.project=" + Context.Config.ProjectName },
        })
        {
            var inspected = Docker(arguments);
            RequireSuccess(inspected, "inspect fixture ownership");
            Require(string.IsNullOrWhiteSpace(inspected.StandardOutput), "The disposable project already has Docker resources; refusing to use them.");
        }

        if (importedCertificate) PrepareImportedCertificate();

        _configuration.BeginGeneration(Context.Config, Target);

        if (!OperatingSystem.IsWindows())
        {
            File.SetUnixFileMode(Target, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        }

        EnvGenerator.WriteFile(Path.Combine(Target, ".env"), Context.Config.EnvironmentValues);
        _configuration.WriteAllConfigFiles(Context.Config, Target);
        var certificate = new CertificateService().EnsureCertificates(Target, Context.Config.PublicEndpoint.Host, Context.Config.Certificates);
        Require(certificate.Reused, "Loading the generated installation must reuse its certificate material.");
        _trustCertificatePath = certificate.TrustCertificatePath;
        WriteRuntimeOverride();

        if (_sourceImages is not null)
        {
            var runtimePlan = Compose(["config", "--format", "json"]);
            RequireSuccess(runtimePlan, "validate source-image runtime composition");
            using var parsed = JsonDocument.Parse(runtimePlan.StandardOutput);
            _sourceImages.ValidateCompose(parsed.RootElement.GetProperty("services"), SourceServices);
        }

        ValidateGeneratedPorts();
        _ownsResources = true;
    }

    public void ValidateGeneratedPorts()
    {
        var rendered = GeneratedCompose(["config", "--format", "json"]);
        RequireSuccess(rendered, "parse generated Compose");
        AssertSinglePublishedPort(rendered.StandardOutput, Context.Config.PublicEndpoint.HttpsPort);

        using var parsed = JsonDocument.Parse(rendered.StandardOutput);
        var capabilities = ServiceCatalog.Resolve(Context.Config.Features);
        var serviceIds = parsed.RootElement.GetProperty("services").EnumerateObject().Select(service => service.Name).ToHashSet(StringComparer.Ordinal);
        Require(serviceIds.SetEquals(capabilities.ServiceIds), "Compose service inventory must match the reviewed capabilities.");

        foreach (var (section, expected) in new[] { ("volumes", capabilities.Volumes), ("networks", capabilities.Networks) })
        {
            var actual = parsed.RootElement.GetProperty(section).EnumerateObject().Select(item => item.Name).ToHashSet(StringComparer.Ordinal);
            Require(actual.SetEquals(expected), $"Compose {section} inventory differs from the reviewed capabilities: " +
                $"missing [{string.Join(", ", expected.Except(actual))}], unexpected [{string.Join(", ", actual.Except(expected))}].");
        }

        foreach (var service in parsed.RootElement.GetProperty("services").EnumerateObject())
        {
            Require(!service.Value.TryGetProperty("container_name", out _), "The fixture must use project-scoped container names.");
        }

        foreach (var kind in new[] { "networks", "volumes" })
        {
            foreach (var resource in parsed.RootElement.GetProperty(kind).EnumerateObject())
            {
                Require(!resource.Value.TryGetProperty("external", out var external) || external.ValueKind == JsonValueKind.False,
                    "The fixture must not use external Docker resources.");
                Require(resource.Value.GetProperty("name").GetString()!.StartsWith(Context.Config.ProjectName + "_", StringComparison.Ordinal),
                    "The fixture must own every named volume and network.");
            }
        }
    }

    public static void AssertSinglePublishedPort(string composeJson, int expectedPort)
    {
        using var parsed = JsonDocument.Parse(composeJson);
        var publications = parsed.RootElement.GetProperty("services").EnumerateObject()
            .Where(service => service.Value.TryGetProperty("ports", out _))
            .SelectMany(service => service.Value.GetProperty("ports").EnumerateArray()
                .Select(port => (Service: service.Name, Port: port)))
            .ToArray();

        Require(publications.Length == 1, "Every generated selection must publish exactly one host TCP port.");
        var publication = publications[0];
        Require(publication.Service == "nginx" && publication.Port.GetProperty("target").GetInt32() == 443 &&
                publication.Port.GetProperty("published").GetString() == expectedPort.ToString(System.Globalization.CultureInfo.InvariantCulture) &&
                publication.Port.GetProperty("protocol").GetString() == "tcp",
            "Only the selected public HTTPS port may be published by Nginx.");
    }

    public void ValidateProxyWithCachedImage()
    {
        var result = Docker([
            "run", "--rm", "--pull", "never", "--network", "none",
            "--add-host", "api:127.0.0.1", "--add-host", "frontend:127.0.0.1",
            "--add-host", "garage:127.0.0.1", "--add-host", "grafana:127.0.0.1",
            "--mount", "type=bind,source=" + Path.Combine(Target, "nginx", "nginx.conf") + ",target=/etc/nginx/nginx.conf,readonly",
            "--mount", "type=bind,source=" + Path.Combine(Target, "nginx", "certs", "server.crt") + ",target=/etc/nginx/certs/server.crt,readonly",
            "--mount", "type=bind,source=" + Path.Combine(Target, "nginx", "certs", "server.key") + ",target=/etc/nginx/certs/server.key,readonly",
            _sourceImages?.Image("nginx") ?? "alexandria-local-nginx:latest", "nginx", "-t",
        ]);
        RequireSuccess(result, "validate generated Nginx configuration with its cached runtime");
    }

    public void Start()
    {
        Require(_ownsResources, "Validate disposable ownership before starting containers.");
        _started = true;
        RequireSuccess(Compose(["up", "--detach", "--pull", "never", "--no-build", .. CachedInfrastructureFixture.CoreServices], TimeSpan.FromMinutes(3)),
            "start disposable infrastructure");

        var docker = new DockerService();
        docker.Configure(Context.Config, _ct);
        Require(docker.WaitForServices(Target, CachedInfrastructureFixture.CoreServices, TimeSpan.FromMinutes(3), CachedInfrastructureFixture.OneShotServices),
            "Disposable infrastructure did not initialize.");

        StorageCapacityReceipt.Commit(Context);
        InstallationOrchestrator.PersistStorageKeys(Context);
        RememberSecrets();
        RequireSuccess(Compose(["up", "--detach", "--pull", "never", "--no-build", "api"], TimeSpan.FromMinutes(2)),
            "start current compiled API");

        Require(docker.WaitForServices(Target, ["api"], TimeSpan.FromMinutes(3), []),
            "Current API bootstrap did not become ready before core workers started.");
        var services = new List<string> { "media-worker", "playlist-worker", "frontend", "nginx" };

        if (Context.Config.Features.IsEnabled(FeatureCatalog.Monitoring.Id))
        {
            string[] monitoring = ["prometheus", "grafana", "cadvisor", "postgres-exporter", "alloy", "loki"];
            RequireSuccess(Compose(["up", "--detach", "--pull", "never", "--no-build", .. monitoring], TimeSpan.FromMinutes(2)),
                "start disposable monitoring");
            RequireServicesReady(docker, monitoring);
            Require(docker.WaitForMonitoring(Target, TimeSpan.FromMinutes(2)), "Internal monitoring readiness did not complete.");
        }

        RequireSuccess(Compose(["up", "--detach", "--pull", "never", "--no-build", .. services], TimeSpan.FromMinutes(3)),
            "start disposable HTTPS application");
        RequireServicesReady(docker, services);
        AssertRuntimePublications();
    }

    private void RequireServicesReady(DockerService docker, IReadOnlyCollection<string> services)
    {
        if (docker.WaitForServices(Target, services, TimeSpan.FromMinutes(3), [])) return;

        RememberSecrets();
        var status = Compose(["ps", "--all", "--format", "json"]);
        var logs = Compose(["logs", "--no-color", "--tail", "20", .. services]);
        throw new InvalidOperationException("Disposable services did not become ready: " + string.Join(", ", services) + ". " +
            _redactor.Redact(status.StandardOutput + "\n" + logs.StandardOutput + logs.StandardError));
    }

    public HttpClient CreateClient(bool authenticatedCookies = true) => new(CreateHandler(authenticatedCookies), disposeHandler: true)
    {
        BaseAddress = Origin,
        Timeout = TimeSpan.FromSeconds(30),
    };

    public void ValidateCoreHealth()
    {
        var result = Compose(["exec", "--no-TTY", "api", "dotnet", "Alexandria.Api.dll", "--healthcheck"]);
        RequireSuccess(result, "verify actual API health with optional processors disabled");
    }

    public SocketsHttpHandler CreateHandler(bool authenticatedCookies = true)
    {
        var trustPath = _trustCertificatePath!;
        Require(File.Exists(trustPath), "Generated certificate authority is required for scoped client trust.");
        var authority = X509CertificateLoader.LoadCertificateFromFile(trustPath);
        var policy = new X509ChainPolicy
        {
            TrustMode = X509ChainTrustMode.CustomRootTrust,
            RevocationMode = X509RevocationMode.NoCheck,
        };
        policy.CustomTrustStore.Add(authority);

        return new SocketsHttpHandler
        {
            AllowAutoRedirect = false,
            UseProxy = false,
            UseCookies = authenticatedCookies,
            CookieContainer = Cookies,
            SslOptions = new SslClientAuthenticationOptions { CertificateChainPolicy = policy },
            ConnectCallback = async (_, ct) =>
            {
                var socket = new Socket(SocketType.Stream, ProtocolType.Tcp);

                try
                {
                    await socket.ConnectAsync(IPAddress.Loopback, Context.Config.PublicEndpoint.HttpsPort, ct);
                    return new NetworkStream(socket, ownsSocket: true);
                }
                catch
                {
                    socket.Dispose();
                    throw;
                }
            },
        };
    }

    public string ContentHash(byte[] payload)
    {
        var output = _sourceImages is null ? ApiOutput : CopySourceBlake3Assets();
        var assembly = Assembly.LoadFrom(Path.Combine(output, "Blake3.dll"));

        if (!NativeResolvers.Contains(assembly))
        {
            var architecture = RuntimeInformation.ProcessArchitecture.ToString().ToLowerInvariant();
            var nativePath = Path.Combine(output, "runtimes", "linux-" + architecture, "native", "libblake3_dotnet.so");
            Require(OperatingSystem.IsLinux() && File.Exists(nativePath), "The selected API artifacts must include the host's Blake3 native runtime.");
            NativeLibrary.SetDllImportResolver(assembly, (_, _, _) => NativeLibrary.Load(nativePath));
            NativeResolvers.Add(assembly);
        }

        var method = assembly.GetType("Blake3.Hasher", throwOnError: true)!
            .GetMethod("Hash", [typeof(ReadOnlySpan<byte>), typeof(Span<byte>)])!;
        var hash = new byte[32];
        method.CreateDelegate<HashBytes>()(payload, hash);

        return Convert.ToHexStringLower(hash);
    }

    private string CopySourceBlake3Assets()
    {
        if (_sourceBlake3Output is not null) return _sourceBlake3Output;

        var output = Path.Combine(Target, "source-image-blake3");
        var architecture = RuntimeInformation.ProcessArchitecture.ToString().ToLowerInvariant();
        var nativeDirectory = Path.Combine(output, "runtimes", "linux-" + architecture, "native");
        Directory.CreateDirectory(nativeDirectory);
        var container = "builder-blake3-" + Guid.NewGuid().ToString("N");
        RequireSuccess(Docker(["create", "--pull", "never", "--network", "none", "--name", container, _sourceImages!.Image("api")]),
            "create disposable source-image artifact reader");

        try
        {
            RequireSuccess(Docker(["cp", container + ":/app/Blake3.dll", Path.Combine(output, "Blake3.dll")]),
                "copy Blake3 assembly from the built API image");
            RequireSuccess(Docker(["cp", container + ":/app/runtimes/linux-" + architecture + "/native/libblake3_dotnet.so",
                Path.Combine(nativeDirectory, "libblake3_dotnet.so")]), "copy Blake3 native runtime from the built API image");
        }
        finally
        {
            RequireSuccess(Docker(["rm", container], ct: CancellationToken.None), "remove disposable source-image artifact reader");
        }

        _sourceBlake3Output = output;
        return output;
    }

    private static readonly HashSet<Assembly> NativeResolvers = [];

    private delegate void HashBytes(ReadOnlySpan<byte> input, Span<byte> output);

    public void RequireSuccess(ProcessResult result, string operation)
    {
        if (result.Succeeded) return;

        RememberSecrets();
        throw new InvalidOperationException($"Could not {operation} (exit {result.ExitCode}, timed out: {result.TimedOut}). " +
            _redactor.Redact(result.StandardError));
    }

    private void AssertRuntimePublications()
    {
        var containers = Compose(["ps", "--all", "--quiet"]);
        RequireSuccess(containers, "inspect fixture containers");
        var ids = containers.StandardOutput.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var inspected = Docker(["inspect", .. ids]);
        RequireSuccess(inspected, "inspect actual Docker port bindings");
        using var parsed = JsonDocument.Parse(inspected.StandardOutput);
        var bindings = parsed.RootElement.EnumerateArray()
            .Select(container => container.GetProperty("HostConfig").GetProperty("PortBindings"))
            .Where(ports => ports.ValueKind == JsonValueKind.Object)
            .SelectMany(ports => ports.EnumerateObject())
            .SelectMany(binding => binding.Value.EnumerateArray())
            .ToArray();

        Require(bindings.Length == 1 && bindings[0].GetProperty("HostIp").GetString() == _bindAddress &&
                bindings[0].GetProperty("HostPort").GetString() == Context.Config.PublicEndpoint.HttpsPort.ToString(System.Globalization.CultureInfo.InvariantCulture),
            "The disposable runtime must publish only its dedicated HTTPS port on its selected interface.");
    }

    private string WorkerOutput(string project) => Path.Combine(Context.SourceRoot, "Backend", project, "bin", "Debug", "net10.0");

    private void WriteRuntimeOverride()
    {
        if (_sourceImages is not null)
        {
            var text = new StringBuilder("services:\n");

            foreach (var service in SourceServices) _sourceImages.AppendOverride(text, service);

            text.AppendLine($$"""
                    ports: !override
                      - "{{_bindAddress}}:{{Context.Config.PublicEndpoint.HttpsPort}}:443"
                    depends_on: !override
                      frontend:
                        condition: service_started
                      api:
                        condition: service_healthy
                """);
            ProtectedFile.WriteAtomic(Path.Combine(Target, "docker-compose.fixture.yml"), text.ToString());
            return;
        }

        var runtimeSettings = Path.Combine(Target, "runtime-settings.json");
        ProtectedFile.WriteAtomic(runtimeSettings, "{}");

        if (!OperatingSystem.IsWindows())
            File.SetUnixFileMode(runtimeSettings, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.GroupRead | UnixFileMode.OtherRead);

        ProtectedFile.WriteAtomic(Path.Combine(Target, "docker-compose.fixture.yml"), $$"""
            services:
              garage-init:
                build: !reset null
                image: alexandria-local-garage-init:latest
                pull_policy: never
                volumes:
                  - {{Yaml(Path.Combine(Context.SourceRoot, "scripts", "garage-init", "init.sh") + ":/scripts/init.sh:ro")}}
              api:
                build: !reset null
                image: alexandria-local-api:latest
                pull_policy: never
                entrypoint: ["dotnet", "Alexandria.Api.dll"]
                environment:
                  ASPNETCORE_CONTENTROOT: /fixture-config
                volumes:
                  - {{Yaml(ApiOutput + ":/app:ro")}}
                  - {{Yaml(runtimeSettings + ":/fixture-config/appsettings.json:ro")}}
              media-worker:
                build: !reset null
                image: alexandria-local-media-worker:latest
                pull_policy: never
                entrypoint: ["dotnet", "Alexandria.Workers.Media.dll"]
                environment:
                  ASPNETCORE_CONTENTROOT: /fixture-config
                volumes:
                  - {{Yaml(WorkerOutput("Alexandria.Workers.Media") + ":/app:ro")}}
                  - {{Yaml(runtimeSettings + ":/fixture-config/appsettings.json:ro")}}
              playlist-worker:
                build: !reset null
                image: alexandria-local-playlist-worker:latest
                pull_policy: never
                entrypoint: ["dotnet", "Alexandria.Workers.Playlist.dll"]
                environment:
                  ASPNETCORE_CONTENTROOT: /fixture-config
                volumes:
                  - {{Yaml(WorkerOutput("Alexandria.Workers.Playlist") + ":/app:ro")}}
                  - {{Yaml(runtimeSettings + ":/fixture-config/appsettings.json:ro")}}
              frontend:
                build: !reset null
                image: alexandria-local-frontend:latest
                pull_policy: never
                volumes:
                  - {{Yaml(FrontendOutput + ":/usr/share/nginx/html:ro")}}
              nginx:
                build: !reset null
                image: alexandria-local-nginx:latest
                pull_policy: never
                ports: !override
                  - "{{_bindAddress}}:{{Context.Config.PublicEndpoint.HttpsPort}}:443"
                depends_on: !override
                  frontend:
                    condition: service_started
                  api:
                    condition: service_healthy
            """);
    }

    private void PrepareImportedCertificate()
    {
        Directory.CreateDirectory(Target);
        using var key = RSA.Create(2048);
        var request = new CertificateRequest("CN=HTTPS fixture", key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        request.CertificateExtensions.Add(new X509BasicConstraintsExtension(false, false, 0, true));
        request.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.DigitalSignature | X509KeyUsageFlags.KeyEncipherment, true));
        request.CertificateExtensions.Add(new X509EnhancedKeyUsageExtension(new OidCollection { new("1.3.6.1.5.5.7.3.1") }, false));
        var names = new SubjectAlternativeNameBuilder();
        names.AddIpAddress(IPAddress.Parse(Context.Config.PublicEndpoint.Host));
        request.CertificateExtensions.Add(names.Build());
        using var certificate = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddMinutes(-5), DateTimeOffset.UtcNow.AddDays(30));
        var sourceCertificate = Path.Combine(Target, "fixture-import.crt");
        var sourceKey = Path.Combine(Target, "fixture-import.key");
        ProtectedFile.WriteAtomic(sourceCertificate, certificate.ExportCertificatePem());
        ProtectedFile.WriteAtomic(sourceKey, key.ExportPkcs8PrivateKeyPem());
        Context.Config.Certificates = new CertificateSettings
        {
            Mode = CertificateSettings.ImportedMode,
            ImportCertificatePath = sourceCertificate,
            ImportPrivateKeyPath = sourceKey,
        };
    }

    public void ValidateAllSelections()
    {
        var features = FeatureCatalog.ToggleFeatures;
        var original = Context.Config.Features;

        try
        {
            for (var mask = 0; mask < (1 << features.Count) * 3; mask++)
            {
                Context.Config.Features = new FeatureSelection
                {
                    Enabled = features.Where((_, bit) => ((mask / 3) & (1 << bit)) != 0).Select(feature => feature.Id).ToHashSet(StringComparer.Ordinal),
                };
                Context.Config.Features.SetAudioBackbone(new[] { "off", "effnet", "maest" }[mask % 3]);
                ProtectedFile.WriteAtomic(Path.Combine(Target, "docker-compose.yml"), _configuration.GenerateDockerCompose(Context.Config));
                ValidateGeneratedPorts();
            }
        }
        finally
        {
            Context.Config.Features = original;
            ProtectedFile.WriteAtomic(Path.Combine(Target, "docker-compose.yml"), _configuration.GenerateDockerCompose(Context.Config));
        }
    }

    private ProcessResult GeneratedCompose(IReadOnlyList<string> arguments) => Docker([
        "compose", "--project-name", Context.Config.ProjectName, "--project-directory", Target,
        "--env-file", Path.Combine(Target, ".env"), "-f", Path.Combine(Target, "docker-compose.yml"), .. arguments,
    ]);

    public ProcessResult Compose(IReadOnlyList<string> arguments, TimeSpan? timeout = null, CancellationToken? ct = null) => Docker([
        "compose", "--project-name", Context.Config.ProjectName, "--project-directory", Target,
        "--env-file", Path.Combine(Target, ".env"), "-f", Path.Combine(Target, "docker-compose.yml"),
        "-f", Path.Combine(Target, "docker-compose.fixture.yml"), .. arguments,
    ], timeout, ct);

    private ProcessResult Docker(IReadOnlyList<string> arguments, TimeSpan? timeout = null, CancellationToken? ct = null)
    {
        var start = new ProcessStartInfo("docker")
        {
            WorkingDirectory = Context.SourceRoot,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };

        foreach (var argument in arguments) start.ArgumentList.Add(argument);

        return _runner.Run(start, timeout ?? TimeSpan.FromSeconds(30), ct ?? _ct);
    }

    private void RememberSecrets()
    {
        foreach (var pair in Context.Config.EnvironmentValues) _secrets[pair.Key] = pair.Value;

        var handoff = Path.Combine(Target, "init-output", "garage-credentials.env");

        if (File.Exists(handoff))
        {
            foreach (var pair in EnvGenerator.Parse(File.ReadAllText(handoff))) _secrets[pair.Key] = pair.Value;
        }
    }

    private static int ReserveAvailablePort()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();

        try { return ((IPEndPoint)listener.LocalEndpoint).Port; }
        finally { listener.Stop(); }
    }

    private static string Yaml(string value) => "'" + value.Replace("$", "$$", StringComparison.Ordinal).Replace("'", "''", StringComparison.Ordinal) + "'";

    private static string FindSourceRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);

        while (current is not null)
        {
            if (Directory.Exists(Path.Combine(current.FullName, "Builder", "Templates", "Local")) &&
                File.Exists(Path.Combine(current.FullName, "scripts", "garage-init", "init.sh"))) return current.FullName;

            current = current.Parent;
        }

        throw new InvalidOperationException("Run HTTPS integration tests from a source checkout.");
    }

    public static void Require(bool condition, string message) => CachedInfrastructureFixture.Require(condition, message);

    public void Dispose()
    {
        try
        {
            if (_started && _ownsResources)
            {
                RequireSuccess(Compose(["down", "--volumes", "--timeout", "10"], TimeSpan.FromMinutes(1), CancellationToken.None),
                    "remove this fixture's disposable resources");
            }

            if (Directory.Exists(Target)) Directory.Delete(Target, recursive: true);
        }
        finally
        {
            Context.Cancellation.Dispose();
        }
    }
}
