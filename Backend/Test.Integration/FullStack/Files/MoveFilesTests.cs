using System.Net;
using System.Net.Http.Json;
using API.Features.Storage.Files.MoveFiles;
using AwesomeAssertions;
using Data.Context;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Test.Common.Fixtures;
using Xunit;

namespace Test.Integration.FullStack.Files;

public class MoveFilesTests(AlexandriaFixture fixture) : FullStackTestBase(fixture)
{
    private const string Route = "/api/files/move";

    [Fact]
    public async Task MoveToDirectory_UpdatesDirectoryId()
    {
        var file = await SeedFileWithVersionAsync();
        var dir = await SeedDirectoryAsync();
        var req = new MoveFilesRequest { FileIds = [file.Id], DestinationId = dir.Id };

        var response = await Auth.PostAsJsonAsync(Route, req, cancellationToken: TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AlexandriaDbContext>();
        var movedFile = await db.Files.FindAsync(new object?[] { file.Id }, TestContext.Current.CancellationToken);
        movedFile!.DirectoryId.Should().Be(dir.Id);
    }

    [Fact]
    public async Task MoveToRoot_SetsDirectoryIdNull()
    {
        var dir = await SeedDirectoryAsync();
        var file = await SeedFileWithVersionAsync(fb => fb.WithDirectory(dir.Id));
        var req = new MoveFilesRequest { FileIds = [file.Id], DestinationId = null };

        var response = await Auth.PostAsJsonAsync(Route, req, cancellationToken: TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AlexandriaDbContext>();
        var movedFile = await db.Files.FindAsync(new object?[] { file.Id }, TestContext.Current.CancellationToken);
        movedFile!.DirectoryId.Should().BeNull();
    }

    [Fact]
    public async Task Unauthenticated_Returns401()
    {
        var req = new MoveFilesRequest { FileIds = [Guid.NewGuid()] };

        var response = await Anon.PostAsJsonAsync(Route, req, cancellationToken: TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task EmptyFileIds_Returns400()
    {
        var req = new MoveFilesRequest { FileIds = [] };

        var response = await Auth.PostAsJsonAsync(Route, req, cancellationToken: TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task DuplicateFileIds_Returns400()
    {
        var id = Guid.NewGuid();
        var req = new MoveFilesRequest { FileIds = [id, id] };

        var response = await Auth.PostAsJsonAsync(Route, req, cancellationToken: TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
