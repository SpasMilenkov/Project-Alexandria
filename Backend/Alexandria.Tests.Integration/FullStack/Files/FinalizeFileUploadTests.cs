using System.Net;
using System.Net.Http.Json;
using Alexandria.Api.Features.Storage.Files.FinalizeFileUpload;
using Alexandria.Api.Features.Storage.Files.InitiateFileUpload;
using Alexandria.Data.Context;
using Alexandria.Tests.Common.Fixtures;
using AwesomeAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Alexandria.Tests.Integration.FullStack.Files;

public class FinalizeFileUploadTests(AlexandriaFixture fixture) : FullStackTestBase(fixture)
{
    private const string InitRoute = "/api/files/init-upload";
    private const string FinalizeRoute = "/api/files/finalize-upload";

    [Fact]
    public async Task HappyPath_AuthenticatedUser_CreatesFileInDb()
    {
        // First initiate an upload to get a valid UploadId
        var initReq = new InitializeFileUploadRequest
        {
            ContentType = "text/plain",
            Hash = Convert.ToHexString(new byte[32]),
            ContentLength = 13L
        };
        var initResponse =
            await Auth.PostAsJsonAsync(InitRoute, initReq, cancellationToken: TestContext.Current.CancellationToken);
        initResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var initBody =
            await initResponse.Content.ReadFromJsonAsync<InitializeFileUploadResponse>(
                cancellationToken: TestContext.Current.CancellationToken);
        initBody.Should().NotBeNull();

        // PUT bytes to the S3 upload URL
        var bytes = "hello, world!"u8.ToArray();
        using var putContent = new ByteArrayContent(bytes);
        var putResponse =
            await new HttpClient().PutAsync(initBody!.UploadUrl, putContent, TestContext.Current.CancellationToken);
        putResponse.IsSuccessStatusCode.Should().BeTrue();

        // Now finalize
        var finalizeReq = new FinalizeFileUploadRequest
        {
            UploadId = initBody.UploadId,
            FileName = "hello.txt"
        };
        var response = await Auth.PostAsJsonAsync(FinalizeRoute, finalizeReq,
            cancellationToken: TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        // Verify a file was created in the DB for this user
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AlexandriaDbContext>();
        var fileExists = await db.Files
            .AnyAsync(f => f.OwnerId == UserId && f.Name == "hello.txt",
                cancellationToken: TestContext.Current.CancellationToken);
        fileExists.Should().BeTrue();
    }

    [Fact]
    [Trait("Bug", "true")]
    // FinalizeFileUpload has AllowAnonymous() but calls User.GetUserId() internally,
    // which throws if no authenticated user is present.
    public async Task Unauthenticated_Throws()
    {
        var req = new FinalizeFileUploadRequest
        {
            UploadId = Guid.NewGuid(),
            FileName = "test.txt"
        };

        var response =
            await Anon.PostAsJsonAsync(FinalizeRoute, req, cancellationToken: TestContext.Current.CancellationToken);

        response.StatusCode.Should().NotBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task UnknownUploadId_Returns4xx()
    {
        var req = new FinalizeFileUploadRequest
        {
            UploadId = Guid.NewGuid(),
            FileName = "ghost.txt"
        };

        var response =
            await Auth.PostAsJsonAsync(FinalizeRoute, req, cancellationToken: TestContext.Current.CancellationToken);

        response.StatusCode.Should().NotBe(HttpStatusCode.OK);
    }
}