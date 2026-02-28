using System.Net;
using System.Net.Http.Json;
using AwesomeAssertions;
using Test.Common.Builders;
using Test.Common.Fixtures;
using Xunit;

namespace Test.Integration.FullStack.Files;

public class DownloadFileByIdTests(AlexandriaFixture fixture) : FullStackTestBase(fixture)
{
    [Fact]
    public async Task HappyPath_ReturnsPresignedUrl()
    {
        var file = await SeedFileWithVersionAsync();

        var response = await Auth.GetAsync($"/api/files/{file.Id}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task Unauthenticated_Returns401()
    {
        var file = await SeedFileWithVersionAsync();

        var response = await Anon.GetAsync($"/api/files/{file.Id}");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task OtherUsersFile_Returns404()
    {
        var (_, otherId) = await CreateOtherUserAsync();
        var file = await SeedFileWithVersionAsync(fb => fb.WithOwner(otherId));

        var response = await Auth.GetAsync($"/api/files/{file.Id}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task NonExistentFile_Returns404()
    {
        var response = await Auth.GetAsync($"/api/files/{Guid.NewGuid()}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
