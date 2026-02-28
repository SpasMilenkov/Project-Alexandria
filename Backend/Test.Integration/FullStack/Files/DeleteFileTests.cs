using System.Net;
using System.Net.Http.Json;
using API.Features.Storage.Files.DeleteFile;
using AwesomeAssertions;
using Data.Context;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Test.Common.Fixtures;
using Xunit;

namespace Test.Integration.FullStack.Files;

public class DeleteFileTests(AlexandriaFixture fixture) : FullStackTestBase(fixture)
{
    private const string Route = "/api/files/";

    private Task<HttpResponseMessage> DeleteFilesAsync(HttpClient client, DeleteFileRequest req)
    {
        var message = new HttpRequestMessage(HttpMethod.Delete, Route)
        {
            Content = JsonContent.Create(req)
        };
        return client.SendAsync(message);
    }

    [Fact]
    public async Task SoftDelete_OwnFile_Returns200()
    {
        var file = await SeedFileWithVersionAsync();
        var req = new DeleteFileRequest { Ids = [file.Id] };

        var response = await DeleteFilesAsync(Auth, req);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task SoftDelete_SetsDeletedAt()
    {
        var file = await SeedFileWithVersionAsync();
        var req = new DeleteFileRequest { Ids = [file.Id] };

        await DeleteFilesAsync(Auth, req);

        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AlexandriaDbContext>();
        var deletedFile = await db.Files.FindAsync(file.Id);
        deletedFile!.DeletedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task Unauthenticated_Returns401()
    {
        var req = new DeleteFileRequest { Ids = [Guid.NewGuid()] };

        var response = await DeleteFilesAsync(Anon, req);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    [Trait("Bug", "true")]
    // DeleteFile uses ExecuteUpdateAsync with no row-count check — non-existent IDs return 200
    public async Task NonExistentFileId_Returns200()
    {
        var req = new DeleteFileRequest { Ids = [Guid.NewGuid()] };

        var response = await DeleteFilesAsync(Auth, req);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task EmptyIds_Returns200()
    {
        var req = new DeleteFileRequest { Ids = [] };

        var response = await DeleteFilesAsync(Auth, req);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
