using System.Net;
using System.Net.Sockets;
using System.Text;
using Alexandria.Common.Health;
using AwesomeAssertions;

namespace Alexandria.Tests.Unit.Infrastructure;

public class HttpHealthProbeTests
{
    [Theory]
    [InlineData(200, 0)]
    [InlineData(204, 0)]
    [InlineData(302, 1)]
    [InlineData(503, 1)]
    public async Task healthcheck_real_http_response_uses_success_status(int status, int expectedExitCode)
    {
        var ct = TestContext.Current.CancellationToken;
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var endpoint = (IPEndPoint)listener.LocalEndpoint;

        var probe = HttpHealthProbe.TryRunAsync(["--healthcheck", $"http://127.0.0.1:{endpoint.Port}/health/ready"], ct);
        using var connection = await listener.AcceptTcpClientAsync(ct);
        await using var stream = connection.GetStream();
        using var reader = new StreamReader(stream, leaveOpen: true);
        (await reader.ReadLineAsync(ct)).Should().Be("GET /health/ready HTTP/1.1");
        while (await reader.ReadLineAsync(ct) is { Length: > 0 }) { }

        var response = Encoding.ASCII.GetBytes($"HTTP/1.1 {status} Fixture\r\nContent-Length: 0\r\nConnection: close\r\n\r\n");
        await stream.WriteAsync(response, ct);

        (await probe).Should().Be(expectedExitCode);
    }

    [Fact]
    public async Task healthcheck_refused_connection_returns_failure()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var endpoint = (IPEndPoint)listener.LocalEndpoint;
        listener.Stop();

        var result = await HttpHealthProbe.TryRunAsync(["--healthcheck", $"http://127.0.0.1:{endpoint.Port}/health"],
            TestContext.Current.CancellationToken);

        result.Should().Be(1);
    }

    [Fact]
    public async Task healthcheck_nonresponsive_server_times_out()
    {
        var ct = TestContext.Current.CancellationToken;
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var endpoint = (IPEndPoint)listener.LocalEndpoint;

        var probe = HttpHealthProbe.TryRunAsync(["--healthcheck", $"http://127.0.0.1:{endpoint.Port}/health"], ct);
        using var connection = await listener.AcceptTcpClientAsync(ct);

        (await probe.WaitAsync(TimeSpan.FromSeconds(10), ct)).Should().Be(1);
    }

    [Fact]
    public async Task healthcheck_cancellation_returns_failure()
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var endpoint = (IPEndPoint)listener.LocalEndpoint;

        var probe = HttpHealthProbe.TryRunAsync(["--healthcheck", $"http://127.0.0.1:{endpoint.Port}/health"], cts.Token);
        using var connection = await listener.AcceptTcpClientAsync(cts.Token);
        cts.Cancel();

        (await probe).Should().Be(1);
    }

    [Theory]
    [InlineData("--urls", "http://127.0.0.1:8080")]
    [InlineData("--healthcheck", "file:///tmp/health")]
    [InlineData("--healthcheck", "invalid")]
    public async Task healthcheck_arguments_only_handle_valid_probe_mode(string argument, string address)
    {
        var result = await HttpHealthProbe.TryRunAsync([argument, address], TestContext.Current.CancellationToken);

        result.Should().Be(argument == "--healthcheck" ? 1 : null);
    }
}
