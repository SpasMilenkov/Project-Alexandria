using Alexandria.Api.Features.Auth.Native;
using AwesomeAssertions;
using Microsoft.AspNetCore.Http;

namespace Alexandria.Tests.Unit.Auth;

public class BrowserRequestsTests
{
    [Theory]
    [InlineData("Origin", "https://alexandria.local")]
    [InlineData("Sec-Fetch-Mode", "cors")]
    [InlineData("Sec-Fetch-Site", "same-origin")]
    public void IsFromBrowser_headerOnlyBrowsersSend_isTrue(string header, string value)
    {
        var request = new DefaultHttpContext().Request;
        request.Headers[header] = value;

        BrowserRequests.IsFromBrowser(request).Should().BeTrue();
    }

    [Fact]
    public void IsFromBrowser_plainNativeRequest_isFalse()
    {
        var request = new DefaultHttpContext().Request;
        request.Headers.UserAgent = "Parchment/0.1 (Linux)";
        request.Headers.Authorization = "Bearer token";

        BrowserRequests.IsFromBrowser(request).Should().BeFalse();
    }
}