using System.Net;
using System.Net.Http.Json;
using Alexandria.Api.Features.Storage.Files.CopyFiles;
using Alexandria.Data.Context;
using Alexandria.Tests.Common.Fixtures;
using AwesomeAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Alexandria.Tests.Integration.FullStack.Files;

public class CopyFilesTests(AlexandriaFixture fixture) : FullStackTestBase(fixture)
{
    private const string Route = "/api/files/copy";

    [Fact]
    public async Task CopyToDirectory_CreatesNewFileInDestination()
    {
        var file = await SeedFileWithVersionAsync();
        var destDir = await SeedDirectoryAsync();
        var req = new CopyFilesRequest { FileIds = [file.Id], DestinationId = destDir.Id };

        var response = await Auth.PostAsJsonAsync(Route, req, cancellationToken: TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AlexandriaDbContext>();
        var fileCount = await db.Files.CountAsync(f => f.OwnerId == UserId,
            cancellationToken: TestContext.Current.CancellationToken);
        fileCount.Should().Be(2); // original + copy
        var copy = await db.Files.FirstOrDefaultAsync(f => f.OwnerId == UserId && f.DirectoryId == destDir.Id,
            cancellationToken: TestContext.Current.CancellationToken);
        copy.Should().NotBeNull();
    }

    [Fact]
    public async Task Unauthenticated_Returns401()
    {
        var req = new CopyFilesRequest { FileIds = [Guid.NewGuid()], DestinationId = Guid.NewGuid() };

        var response = await Anon.PostAsJsonAsync(Route, req, cancellationToken: TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task EmptyFileIds_Returns400()
    {
        var req = new CopyFilesRequest { FileIds = [], DestinationId = Guid.NewGuid() };

        var response = await Auth.PostAsJsonAsync(Route, req, cancellationToken: TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task DuplicateFileIds_Returns400()
    {
        var id = Guid.NewGuid();
        var req = new CopyFilesRequest { FileIds = [id, id], DestinationId = Guid.NewGuid() };

        var response = await Auth.PostAsJsonAsync(Route, req, cancellationToken: TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}