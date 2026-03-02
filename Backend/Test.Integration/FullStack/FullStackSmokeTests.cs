using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AwesomeAssertions;
using Test.Common.Fixtures;
using Xunit;

namespace Test.Integration.FullStack;

public class FullStackSmokeTests(AlexandriaFixture fixture) : FullStackTestBase(fixture)
{
    [Fact]
    public async Task GetFiles_Unauthenticated_ShouldReturn401()
    {
        var response = await Anon.GetAsync("/api/files/root", TestContext.Current.CancellationToken);
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetFiles_Authenticated_ShouldReturn200WithEmptyList()
    {
        var response = await Auth.GetAsync("/api/files/root?page=1&pageSize=10", TestContext.Current.CancellationToken);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: TestContext.Current.CancellationToken);
        body.GetProperty("items").GetArrayLength().Should().Be(0);
    }

    [Fact]
    public async Task InitiateUpload_ShouldReturn200_WithUploadUrl()
    {
        var response = await Auth.PostAsJsonAsync("/api/files/init-upload", new
        {
            ContentType = "text/plain",
            ContentLength = 1024L,
            Hash = Convert.ToHexString(new byte[32])
        }, cancellationToken: TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<InitiateUploadResponse>(cancellationToken: TestContext.Current.CancellationToken);
        body.Should().NotBeNull();
        body!.UploadId.Should().NotBeEmpty();
        body.UploadUrl.Should().StartWith("http");
    }

    private sealed record InitiateUploadResponse(Guid UploadId, string UploadUrl);
}