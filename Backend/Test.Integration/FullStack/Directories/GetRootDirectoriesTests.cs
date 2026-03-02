using System.Net;
using System.Net.Http.Json;
using AwesomeAssertions;
using DTO.Directories;
using DTO.Files;
using Test.Common.Fixtures;
using Xunit;

namespace Test.Integration.FullStack.Directories;

public class GetRootDirectoriesTests(AlexandriaFixture fixture) : FullStackTestBase(fixture)
{
    private const string Route = "/api/directories/root?page=1&pageSize=10&sortBy=Name&sortDirection=Asc";

    [Fact]
    public async Task NoDirectories_ReturnsEmpty()
    {
        var response = await Auth.GetAsync(Route, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<PaginatedResult<DirectorySummaryDto>>(cancellationToken: TestContext.Current.CancellationToken);
        body!.Items.Count.Should().Be(0);
    }

    [Fact]
    public async Task RootDirectories_AreReturned()
    {
        await SeedDirectoryAsync();
        await SeedDirectoryAsync();

        var response = await Auth.GetAsync(Route, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<PaginatedResult<DirectorySummaryDto>>(cancellationToken: TestContext.Current.CancellationToken);
        body!.TotalCount.Should().Be(2);
    }

    [Fact]
    public async Task Subdirectories_AreExcluded()
    {
        var parent = await SeedDirectoryAsync();
        await SeedDirectoryAsync(parentId: parent.Id);

        var response = await Auth.GetAsync(Route, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<PaginatedResult<DirectorySummaryDto>>(cancellationToken: TestContext.Current.CancellationToken);
        body!.TotalCount.Should().Be(1);
        body.Items.Should().ContainSingle(d => d.Id == parent.Id);
    }

    [Fact]
    public async Task OtherUsersDirectories_AreExcluded()
    {
        var (_, otherId) = await CreateOtherUserAsync();
        await SeedDirectoryAsync(configure: b => b.WithOwner(otherId));

        var response = await Auth.GetAsync(Route, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<PaginatedResult<DirectorySummaryDto>>(cancellationToken: TestContext.Current.CancellationToken);
        body!.TotalCount.Should().Be(0);
    }

    [Fact]
    public async Task Unauthenticated_Returns401()
    {
        var response = await Anon.GetAsync(Route, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
