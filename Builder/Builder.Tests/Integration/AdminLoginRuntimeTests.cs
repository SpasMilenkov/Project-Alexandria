using System.Net;
using System.Net.Sockets;
using AwesomeAssertions;
using Builder.Models;
using Builder.Services;

namespace Builder.Tests.Integration;

public sealed class AdminLoginRuntimeTests
{
    [DockerFact]
    [Trait("Category", "Docker")]
    public async Task FreshAdmin_actualHttpsAuthentication_acceptsEnteredCredentialsAndRejectsWrongPasswordWithoutChangingAccount()
    {
        var ct = TestContext.Current.CancellationToken;
        using var fixture = new CachedHttpsFixture();
        fixture.Prepare();
        fixture.Start();
        using var client = fixture.CreateClient();
        await WaitForApi(client, ct);

        HttpMessageHandler CreateHandler(InstallationConfig config, CookieContainer cookies)
        {
            var handler = HttpsEndpointVerifier.CreateHandler(config, cookies);
            handler.ConnectCallback = async (_, cancellationToken) =>
            {
                var socket = new Socket(SocketType.Stream, ProtocolType.Tcp);

                try
                {
                    await socket.ConnectAsync(IPAddress.Loopback, fixture.Context.Config.PublicEndpoint.HttpsPort, cancellationToken);

                    return new NetworkStream(socket, ownsSocket: true);
                }
                catch
                {
                    socket.Dispose();
                    throw;
                }
            };

            return handler;
        }

        new HttpsEndpointVerifier(CreateHandler).Verify(fixture.Context.Config, ct);
        var verifier = new AdminLoginVerifier(CreateHandler);
        verifier.Verify(fixture.Context.Config, ct);
        var password = fixture.Context.Config.EnvironmentValues["ADMIN_PASSWORD"];

        try
        {
            fixture.Context.Config.EnvironmentValues["ADMIN_PASSWORD"] = password + "incorrect";
            var failure = Assert.Throws<InvalidOperationException>(() => verifier.Verify(fixture.Context.Config, ct));
            failure.Message.Should().Contain("HTTP 401");
            failure.ToString().Should().NotContain(password);
        }
        finally
        {
            fixture.Context.Config.EnvironmentValues["ADMIN_PASSWORD"] = password;
        }

        verifier.Verify(fixture.Context.Config, ct);
        fixture.ValidateCoreHealth();
    }

    private static async Task WaitForApi(HttpClient client, CancellationToken ct)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(ct);
        deadline.CancelAfter(TimeSpan.FromSeconds(30));

        while (true)
        {
            using var response = await client.GetAsync("/api/status", deadline.Token);

            if (response.StatusCode == HttpStatusCode.OK) return;

            await Task.Delay(TimeSpan.FromMilliseconds(250), deadline.Token);
        }
    }
}
