using System.Net;
using System.Net.Http.Json;
using AwesomeAssertions;
using DTO.Files;
using Test.Common.Builders;
using Test.Common.Fixtures;
using Xunit;

namespace Test.Integration.FullStack.Files;

public class GetRootFilesTests(AlexandriaFixture fixture) : FullStackTestBase(fixture)
{
    private const string Route = "/api/files/root?page=1&pageSize=10&sortBy=Name&sortDirection=Asc";

    [Fact]
    public async Task NoFiles_ReturnsEmpty()
    {
        var response = await Auth.GetAsync(Route);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<PaginatedResult<FileResult>>();
        body!.Items.Count.Should().Be(0);
    }

    [Fact]
    public async Task RootFiles_AreReturned()
    {
        await SeedFileWithVersionAsync(fb => fb.WithDirectory(null));
        await SeedFileWithVersionAsync(fb => fb.WithDirectory(null));

        var response = await Auth.GetAsync(Route);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<PaginatedResult<FileResult>>();
        body!.TotalCount.Should().Be(2);
    }

    [Fact]
    public async Task DirectoryFiles_AreExcluded()
    {
        var dir = await SeedDirectoryAsync();
        await SeedFileWithVersionAsync(fb => fb.WithDirectory(dir.Id));

        var response = await Auth.GetAsync(Route);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<PaginatedResult<FileResult>>();
        body!.TotalCount.Should().Be(0);
    }

    [Fact]
    public async Task OtherUsersFiles_AreExcluded()
    {
        var (_, otherId) = await CreateOtherUserAsync();
        await SeedFileWithVersionAsync(fb => fb.WithOwner(otherId).WithDirectory(null));

        var response = await Auth.GetAsync(Route);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<PaginatedResult<FileResult>>();
        body!.TotalCount.Should().Be(0);
    }

    [Fact]
    public async Task Unauthenticated_Returns401()
    {
        var response = await Anon.GetAsync(Route);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
