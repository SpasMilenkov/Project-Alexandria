using System.Net;
using AwesomeAssertions;
using Data.Context;
using Microsoft.Extensions.DependencyInjection;
using Test.Common.Fixtures;
using Xunit;

namespace Test.Integration.FullStack.Directories;

public class DeleteDirectoryTests(AlexandriaFixture fixture) : FullStackTestBase(fixture)
{
    private static string Route(Guid id, bool force = false) =>
        $"/api/directories/{id}?force={force}";

    [Fact]
    public async Task DeleteEmptyDirectory_Returns200()
    {
        var dir = await SeedDirectoryAsync();

        var response = await Auth.DeleteAsync(Route(dir.Id));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AlexandriaDbContext>();
        var deleted = await db.Directories.FindAsync(dir.Id);
        deleted!.DeletedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task DeleteNonEmptyWithoutForce_Returns409()
    {
        var parent = await SeedDirectoryAsync();
        await SeedDirectoryAsync(parentId: parent.Id); // child

        var response = await Auth.DeleteAsync(Route(parent.Id, force: false));

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task DeleteNonEmptyWithForce_Returns200_AndSoftDeletesAll()
    {
        var parent = await SeedDirectoryAsync();
        var child = await SeedDirectoryAsync(parentId: parent.Id);

        var response = await Auth.DeleteAsync(Route(parent.Id, force: true));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AlexandriaDbContext>();
        var deletedParent = await db.Directories.FindAsync(parent.Id);
        var deletedChild = await db.Directories.FindAsync(child.Id);
        deletedParent!.DeletedAt.Should().NotBeNull();
        deletedChild!.DeletedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task Unauthenticated_Returns401()
    {
        var dir = await SeedDirectoryAsync();

        var response = await Anon.DeleteAsync(Route(dir.Id));

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task NonExistentDirectory_Returns4xx()
    {
        var response = await Auth.DeleteAsync(Route(Guid.NewGuid()));

        response.StatusCode.Should().NotBe(HttpStatusCode.OK);
    }
}
