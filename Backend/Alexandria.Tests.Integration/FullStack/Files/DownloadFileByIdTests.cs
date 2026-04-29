using System.Net;
using Alexandria.Tests.Common.Fixtures;
using AwesomeAssertions;
using Xunit;

namespace Alexandria.Tests.Integration.FullStack.Files;

public class DownloadFileByIdTests(AlexandriaFixture fixture) : FullStackTestBase(fixture)
{
    [Fact]
    public async Task HappyPath_ReturnsPresignedUrl()
    {
        var file = await SeedFileWithVersionAsync();

        var response = await Auth.GetAsync($"/api/files/{file.Id}", TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        body.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task Unauthenticated_Returns401()
    {
        var file = await SeedFileWithVersionAsync();

        var response = await Anon.GetAsync($"/api/files/{file.Id}", TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task OtherUsersFile_Returns404()
    {
        var (_, otherId) = await CreateOtherUserAsync();
        var file = await SeedFileWithVersionAsync(fb => fb.WithOwner(otherId));

        var response = await Auth.GetAsync($"/api/files/{file.Id}", TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task NonExistentFile_Returns404()
    {
        var response = await Auth.GetAsync($"/api/files/{Guid.NewGuid()}", TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}