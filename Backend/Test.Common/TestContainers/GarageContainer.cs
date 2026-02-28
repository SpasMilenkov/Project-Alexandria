using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Configurations;
using DotNet.Testcontainers.Containers;

namespace Test.Common.TestContainers;

public sealed class GarageContainer : IAsyncDisposable
{
    private readonly IContainer _container = new ContainerBuilder("dxflrs/garage:v2.2.0")
        .WithPortBinding(3900, true)
        .WithBindMount(GetGarageTomlPath(), "/etc/garage.toml", AccessMode.ReadOnly)
        .WithEnvironment("RUST_LOG", "garage=warn")
        .WithWaitStrategy(
            Wait.ForUnixContainer()
                .UntilCommandIsCompleted("/garage", "status"))
        .WithCleanUp(true)
        .Build();

    public string Endpoint { get; private set; } = null!;
    public string AccessKey { get; private set; } = null!;
    public string SecretKey { get; private set; } = null!;
    public string Region => "garage";

    private static readonly string[] Buckets =
    [
        "alexandria-files",
        "alexandria-previews",
        "alexandria-temp",
        "alexandria-images"
    ];

    public async Task StartAsync(CancellationToken ct = default)
    {
        await _container.StartAsync(ct);
        Endpoint = $"http://localhost:{_container.GetMappedPublicPort(3900)}";
        await InitializeAsync(ct);
    }

    private async Task InitializeAsync(CancellationToken ct)
    {
        var nodeId = await GetNodeIdAsync(ct);
        await AssignLayoutAsync(nodeId, ct);
        await ApplyLayoutAsync(ct);
        await WaitForLayoutReadyAsync(ct);
        await CreateBucketsAsync(ct);
        await CreateAccessKeyAsync(ct);
        await GrantPermissionsAsync(ct);

        if (string.IsNullOrWhiteSpace(AccessKey) || string.IsNullOrWhiteSpace(SecretKey))
            throw new InvalidOperationException(
                "Garage container initialized but credentials are empty. " +
                "The output format of '/garage key create' may have changed. " +
                "Check the raw stdout by enabling debug logging in GarageContainer.");
    }

    private async Task<string> GetNodeIdAsync(CancellationToken ct)
    {
        var result = await _container.ExecAsync(["/garage", "node", "id"], ct);
        EnsureSuccess(result, "node id");

        // Output is just the raw node ID: "abc123@127.0.0.1:3901"
        var line = result.Stdout
                       .Split('\n', StringSplitOptions.RemoveEmptyEntries)
                       .FirstOrDefault(l => l.Contains('@'))
                   ?? throw new InvalidOperationException(
                       $"Could not parse node ID from garage node id output:\n{result.Stdout}");

        return line.Trim().Split('@')[0];
    }

    private async Task AssignLayoutAsync(string nodeId, CancellationToken ct)
    {
        var result = await _container.ExecAsync(
            ["/garage", "layout", "assign", "-z", "dc1", "-c", "1G", nodeId], ct);
        EnsureSuccess(result, "layout assign");
    }

    private async Task ApplyLayoutAsync(CancellationToken ct)
    {
        var result = await _container.ExecAsync(
            ["/garage", "layout", "apply", "--version", "1"], ct);
        EnsureSuccess(result, "layout apply");
    }

    /// <summary>
    /// Polls 'garage status' until the node reports as healthy,
    /// rather than sleeping a fixed amount of time.
    /// </summary>
    private async Task WaitForLayoutReadyAsync(CancellationToken ct)
    {
        const int maxAttempts = 20;
        const int delayMs = 500;

        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            var result = await _container.ExecAsync(["/garage", "status"], ct);

            // Garage reports a healthy layout when it prints the node ID
            // in the status output without any "FAILED" or "layout not configured" lines
            var stdout = result.Stdout + result.Stderr;

            if (result.ExitCode == 0
                && !stdout.Contains("No nodes")
                && !stdout.Contains("layout not configured")
                && !stdout.Contains("FAILED"))
            {
                return;
            }

            if (attempt == maxAttempts)
                throw new InvalidOperationException(
                    $"Garage layout did not become ready after {maxAttempts} attempts.\n" +
                    $"Last status output:\n{stdout}");

            await Task.Delay(delayMs, ct);
        }
    }

    private async Task CreateBucketsAsync(CancellationToken ct)
    {
        foreach (var bucket in Buckets)
        {
            var result = await _container.ExecAsync(
                ["/garage", "bucket", "create", bucket], ct);
            EnsureSuccess(result, $"bucket create {bucket}");
        }
    }

    private async Task CreateAccessKeyAsync(CancellationToken ct)
    {
        var result = await _container.ExecAsync(
            ["/garage", "key", "create", "test-master-key"], ct);
        EnsureSuccess(result, "key create");

        var lines = result.Stdout.Split('\n', StringSplitOptions.RemoveEmptyEntries);

        var keyIdLine = lines.FirstOrDefault(l => l.Contains("Key ID:"))
                        ?? throw new InvalidOperationException(
                            $"Could not find 'Key ID:' in garage key create output:\n{result.Stdout}");

        var secretLine = lines.FirstOrDefault(l => l.Contains("Secret key:"))
                         ?? throw new InvalidOperationException(
                             $"Could not find 'Secret key:' in garage key create output:\n{result.Stdout}");

        AccessKey = keyIdLine.Split(':', 2)[1].Trim();
        SecretKey = secretLine.Split(':', 2)[1].Trim();
    }

    private async Task GrantPermissionsAsync(CancellationToken ct)
    {
        foreach (var bucket in Buckets)
        {
            var result = await _container.ExecAsync(
            [
                "/garage", "bucket", "allow",
                "--read", "--write", "--owner",
                bucket,
                "--key", "test-master-key"
            ], ct);
            EnsureSuccess(result, $"bucket allow {bucket}");
        }
    }

    private static void EnsureSuccess(ExecResult result, string command)
    {
        if (result.ExitCode != 0)
            throw new InvalidOperationException(
                $"Garage command '{command}' failed with exit code {result.ExitCode}.\n" +
                $"Stdout: {result.Stdout}\n" +
                $"Stderr: {result.Stderr}");
    }

    private static string GetGarageTomlPath()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);

        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "garage.toml")))
            dir = dir.Parent;

        return dir is not null
            ? Path.Combine(dir.FullName, "garage.toml")
            : throw new FileNotFoundException(
                "Could not find garage.toml by walking up from the test assembly directory. " +
                "Ensure garage.toml is in the repository root.");
    }

    public ValueTask DisposeAsync() => _container.DisposeAsync();
}