using System.Net;
using System.Net.Http.Json;
using API.Features.Storage.Files.FinalizeFileUpload;
using API.Features.Storage.Files.InitiateFileUpload;
using AwesomeAssertions;
using Data.Context;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Test.Common.Fixtures;
using Xunit;

namespace Test.Integration.FullStack.Files;

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
        var initResponse = await Auth.PostAsJsonAsync(InitRoute, initReq);
        initResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var initBody = await initResponse.Content.ReadFromJsonAsync<InitializeFileUploadResponse>();
        initBody.Should().NotBeNull();

        // PUT bytes to the S3 upload URL
        var bytes = "hello, world!"u8.ToArray();
        using var putContent = new ByteArrayContent(bytes);
        var putResponse = await new HttpClient().PutAsync(initBody!.UploadUrl, putContent);
        putResponse.IsSuccessStatusCode.Should().BeTrue();

        // Now finalize
        var finalizeReq = new FinalizeFileUploadRequest
        {
            UploadId = initBody.UploadId,
            FileName = "hello.txt"
        };
        var response = await Auth.PostAsJsonAsync(FinalizeRoute, finalizeReq);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        // Verify a file was created in the DB for this user
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AlexandriaDbContext>();
        var fileExists = await db.Files
            .AnyAsync(f => f.OwnerId == UserId && f.Name == "hello.txt");
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

        var response = await Anon.PostAsJsonAsync(FinalizeRoute, req);

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

        var response = await Auth.PostAsJsonAsync(FinalizeRoute, req);

        response.StatusCode.Should().NotBe(HttpStatusCode.OK);
    }
}
