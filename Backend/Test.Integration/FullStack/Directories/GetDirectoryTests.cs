using System.Net;
using System.Net.Http.Json;
using API.Features.Storage.Directories.GetDir;
using AwesomeAssertions;
using Test.Common.Builders;
using Test.Common.Fixtures;
using Xunit;

namespace Test.Integration.FullStack.Directories;

public class GetDirectoryTests(AlexandriaFixture fixture) : FullStackTestBase(fixture)
{
    private static string Route(Guid directoryId) => $"/api/directories?directoryId={directoryId}";

    [Fact]
    public async Task GetOwnDirectory_Returns200()
    {
        var dir = await SeedDirectoryAsync();

        var response = await Auth.GetAsync(Route(dir.Id));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<GetDirResult>();
        body!.Directory.Id.Should().Be(dir.Id);
        body.Directory.Name.Should().Be(dir.Name);
    }

    [Fact]
    public async Task Unauthenticated_Returns401()
    {
        var dir = await SeedDirectoryAsync();

        var response = await Anon.GetAsync(Route(dir.Id));

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task OtherUsersDirectory_Returns4xx()
    {
        var (_, otherId) = await CreateOtherUserAsync();
        var dir = await SeedDirectoryAsync(configure: b => b.WithOwner(otherId));

        var response = await Auth.GetAsync(Route(dir.Id));

        response.StatusCode.Should().NotBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task NonExistentDirectory_Returns4xx()
    {
        var response = await Auth.GetAsync(Route(Guid.NewGuid()));

        response.StatusCode.Should().NotBe(HttpStatusCode.OK);
    }
}
