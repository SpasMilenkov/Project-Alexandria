using System.Net;
using System.Net.Http.Json;
using Alexandria.Api.Features.Storage.Directories.MoveDir;
using Alexandria.Data.Context;
using Alexandria.Tests.Common.Fixtures;
using AwesomeAssertions;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Alexandria.Tests.Integration.FullStack.Directories;

public class MoveDirectoryTests(AlexandriaFixture fixture) : FullStackTestBase(fixture)
{
    private const string Route = "/api/directories/move";

    [Fact]
    public async Task MoveToNewParent_UpdatesParentId()
    {
        var dir = await SeedDirectoryAsync();
        var dest = await SeedDirectoryAsync();
        var req = new MoveDirRequest { DirectoryIds = [dir.Id], DestinationId = dest.Id };

        var response = await Auth.PostAsJsonAsync(Route, req, cancellationToken: TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AlexandriaDbContext>();
        var moved = await db.Directories.FindAsync(new object?[] { dir.Id }, TestContext.Current.CancellationToken);
        moved!.ParentId.Should().Be(dest.Id);
    }

    [Fact]
    public async Task MoveToRoot_SetsParentIdNull()
    {
        var parent = await SeedDirectoryAsync();
        var dir = await SeedDirectoryAsync(parentId: parent.Id);
        var req = new MoveDirRequest { DirectoryIds = [dir.Id], DestinationId = null };

        var response = await Auth.PostAsJsonAsync(Route, req, cancellationToken: TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AlexandriaDbContext>();
        var moved = await db.Directories.FindAsync(new object?[] { dir.Id }, TestContext.Current.CancellationToken);
        moved!.ParentId.Should().BeNull();
    }

    [Fact]
    public async Task Unauthenticated_Returns401()
    {
        var req = new MoveDirRequest { DirectoryIds = [Guid.NewGuid()] };

        var response = await Anon.PostAsJsonAsync(Route, req, cancellationToken: TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}