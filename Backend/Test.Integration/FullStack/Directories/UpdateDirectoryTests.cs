using System.Net;
using System.Net.Http.Json;
using API.Features.Storage.Directories.UpdateDir;
using AwesomeAssertions;
using Test.Common.Fixtures;
using Xunit;

namespace Test.Integration.FullStack.Directories;

public class UpdateDirectoryTests(AlexandriaFixture fixture) : FullStackTestBase(fixture)
{
    private const string Route = "/api/directories";

    [Fact]
    public async Task Rename_Returns200_WithNewName()
    {
        var dir = await SeedDirectoryAsync();
        var req = new UpdateDirRequest { DirectoryId = dir.Id, Name = "renamed-dir" };

        var response = await Auth.PutAsJsonAsync(Route, req);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<UpdateDirResponse>();
        body!.Directory.Name.Should().Be("renamed-dir");
    }

    [Fact]
    public async Task Unauthenticated_Returns401()
    {
        var dir = await SeedDirectoryAsync();
        var req = new UpdateDirRequest { DirectoryId = dir.Id, Name = "new-name" };

        var response = await Anon.PutAsJsonAsync(Route, req);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task OtherUsersDirectory_Returns4xx()
    {
        var (_, otherId) = await CreateOtherUserAsync();
        var dir = await SeedDirectoryAsync(configure: b => b.WithOwner(otherId));
        var req = new UpdateDirRequest { DirectoryId = dir.Id, Name = "hacked" };

        var response = await Auth.PutAsJsonAsync(Route, req);

        response.StatusCode.Should().NotBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task NonExistentDirectory_Returns4xx()
    {
        var req = new UpdateDirRequest { DirectoryId = Guid.NewGuid(), Name = "ghost" };

        var response = await Auth.PutAsJsonAsync(Route, req);

        response.StatusCode.Should().NotBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task EmptyName_Returns400()
    {
        var dir = await SeedDirectoryAsync();
        var req = new UpdateDirRequest { DirectoryId = dir.Id, Name = "" };

        var response = await Auth.PutAsJsonAsync(Route, req);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
