using System.Net;
using System.Net.Http.Json;
using Alexandria.Api.Features.Storage.Files.InitiateFileUpload;
using Alexandria.Tests.Common.Fixtures;
using AwesomeAssertions;
using Xunit;

namespace Alexandria.Tests.Integration.FullStack.Files;

public class InitiateFileUploadTests(AlexandriaFixture fixture) : FullStackTestBase(fixture)
{
    private const string Route = "/api/files/init-upload";

    [Fact]
    public async Task HappyPath_ReturnsUploadIdAndUrl()
    {
        var req = new InitializeFileUploadRequest
        {
            ContentType = "text/plain",
            Hash = Convert.ToHexString(new byte[32]),
            ContentLength = 1024L
        };

        var response = await Auth.PostAsJsonAsync(Route, req, cancellationToken: TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<InitializeFileUploadResponse>(
            cancellationToken: TestContext.Current.CancellationToken);
        body.Should().NotBeNull();
        body.UploadId.Should().NotBeEmpty();
        body.UploadUrl.Should().StartWith("http");
    }

    [Fact]
    public async Task HappyPath_WithDirectoryId_Returns200()
    {
        var dir = await SeedDirectoryAsync();
        var req = new InitializeFileUploadRequest
        {
            ContentType = "image/png",
            Hash = Convert.ToHexString(new byte[32]),
            ContentLength = 2048L,
            DirectoryId = dir.Id
        };

        var response = await Auth.PostAsJsonAsync(Route, req, cancellationToken: TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Unauthenticated_Returns401()
    {
        var req = new InitializeFileUploadRequest
        {
            ContentType = "text/plain",
            Hash = Convert.ToHexString(new byte[32]),
            ContentLength = 1024L
        };

        var response = await Anon.PostAsJsonAsync(Route, req, cancellationToken: TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task EmptyContentType_Returns400()
    {
        var req = new InitializeFileUploadRequest
        {
            ContentType = "",
            Hash = Convert.ToHexString(new byte[32]),
            ContentLength = 1024L
        };

        var response = await Auth.PostAsJsonAsync(Route, req, cancellationToken: TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task InvalidHash_TooShort_Returns400()
    {
        var req = new InitializeFileUploadRequest
        {
            ContentType = "text/plain",
            Hash = "abc",
            ContentLength = 1024L
        };

        var response = await Auth.PostAsJsonAsync(Route, req, cancellationToken: TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task InvalidHash_NotHex_Returns400()
    {
        var req = new InitializeFileUploadRequest
        {
            ContentType = "text/plain",
            Hash = new string('Z', 64),
            ContentLength = 1024L
        };

        var response = await Auth.PostAsJsonAsync(Route, req, cancellationToken: TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task EmptyGuidDirectoryId_Returns400()
    {
        var req = new InitializeFileUploadRequest
        {
            ContentType = "text/plain",
            Hash = Convert.ToHexString(new byte[32]),
            ContentLength = 1024L,
            DirectoryId = Guid.Empty
        };

        var response = await Auth.PostAsJsonAsync(Route, req, cancellationToken: TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task NullDirectoryId_IsAllowed()
    {
        var req = new InitializeFileUploadRequest
        {
            ContentType = "text/plain",
            Hash = Convert.ToHexString(new byte[32]),
            ContentLength = 1024L,
            DirectoryId = null
        };

        var response = await Auth.PostAsJsonAsync(Route, req, cancellationToken: TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}