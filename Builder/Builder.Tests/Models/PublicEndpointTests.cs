using AwesomeAssertions;
using Builder.Models;

namespace Builder.Tests.Models;

public class PublicEndpointTests
{
    [Theory]
    [InlineData("192.168.0.106", 443, "https://192.168.0.106")]
    [InlineData(" Alexandria.Example. ", 8443, "https://alexandria.example:8443")]
    [InlineData("bücher.example", 9443, "https://xn--bcher-kva.example:9443")]
    public void Origin_validLanHost_normalizesAndPreservesNondefaultPort(string host, int port, string expected)
    {
        new PublicEndpoint { Host = host, HttpsPort = port }.Origin.Should().Be(expected);
    }

    [Theory]
    [InlineData("")]
    [InlineData("localhost")]
    [InlineData("api.localhost")]
    [InlineData("127.0.0.1")]
    [InlineData("0.0.0.0")]
    [InlineData("224.0.0.1")]
    [InlineData("192.168.1")]
    [InlineData("192.168.0.010")]
    [InlineData("999.1.1.1")]
    [InlineData("https://192.168.0.106")]
    [InlineData("192.168.0.106:8443")]
    [InlineData("192.168.0.106/path")]
    [InlineData("user@host")]
    [InlineData("bad_host.example")]
    [InlineData("*.example")]
    [InlineData("::1")]
    [InlineData("[fd00::1]")]
    public void Validate_unsupportedAddress_rejectsBeforeArtifacts(string host)
    {
        var endpoint = new PublicEndpoint { Host = host };

        var action = endpoint.Validate;

        action.Should().Throw<InvalidOperationException>();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(65536)]
    public void Validate_outOfRangePort_rejects(int port)
    {
        var endpoint = new PublicEndpoint { Host = "192.168.0.106", HttpsPort = port };

        var action = endpoint.Validate;

        action.Should().Throw<InvalidOperationException>();
    }
}
