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
        var req = new UpdateDirRequest { Name = "renamed-dir" };

        var response = await Auth.PatchAsJsonAsync($"{Route}/{dir.Id}", req, cancellationToken: TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<UpdateDirResponse>(cancellationToken: TestContext.Current.CancellationToken);
        body!.Directory.Name.Should().Be("renamed-dir");
    }

    [Fact]
    public async Task Unauthenticated_Returns401()
    {
        var dir = await SeedDirectoryAsync();
        var req = new UpdateDirRequest { Name = "new-name" };

        var response = await Anon.PatchAsJsonAsync($"{Route}/{dir.Id}", req, cancellationToken: TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task OtherUsersDirectory_Returns4xx()
    {
        var (_, otherId) = await CreateOtherUserAsync();
        var dir = await SeedDirectoryAsync(configure: b => b.WithOwner(otherId));
        var req = new UpdateDirRequest { Name = "hacked" };

        var response = await Auth.PatchAsJsonAsync($"{Route}/{dir.Id}", req, cancellationToken: TestContext.Current.CancellationToken);

        response.StatusCode.Should().NotBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task NonExistentDirectory_Returns4xx()
    {
        var nonExistentId = Guid.NewGuid();
        var req = new UpdateDirRequest { Name = "ghost" };

        var response = await Auth.PatchAsJsonAsync($"{Route}/{nonExistentId}", req, cancellationToken: TestContext.Current.CancellationToken);

        response.StatusCode.Should().NotBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task EmptyName_Returns400()
    {
        var dir = await SeedDirectoryAsync();
        var req = new UpdateDirRequest { Name = "" };

        var response = await Auth.PatchAsJsonAsync($"{Route}/{dir.Id}", req, cancellationToken: TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
