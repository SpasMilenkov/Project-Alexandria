using System.Net;
using System.Net.Http.Json;
using Alexandria.Data.Models.Enumerators;
using Alexandria.Dto.Directories;
using Alexandria.Dto.Files;
using Alexandria.Tests.Common.Fixtures;
using AwesomeAssertions;
using Xunit;

namespace Alexandria.Tests.Integration.FullStack.Directories;

public class GetSubdirectoriesTests(AlexandriaFixture fixture) : FullStackTestBase(fixture)
{
    private static string Route(Guid directoryId) =>
        $"/api/directories/sub?directoryId={directoryId}&page=1&pageSize=10&sortBy={SortBy.Name}&sortDirection={SortDirection.Asc}";

    [Fact]
    public async Task DirectoryWithChildren_ReturnsChildren()
    {
        var parent = await SeedDirectoryAsync();
        await SeedDirectoryAsync(parentId: parent.Id);
        await SeedDirectoryAsync(parentId: parent.Id);

        var response = await Auth.GetAsync(Route(parent.Id), TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<PaginatedResult<DirectorySummaryDto>>(
            cancellationToken: TestContext.Current.CancellationToken);
        body!.TotalCount.Should().Be(2);
    }

    [Fact]
    public async Task EmptyDirectory_ReturnsEmpty()
    {
        var dir = await SeedDirectoryAsync();

        var response = await Auth.GetAsync(Route(dir.Id), TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<PaginatedResult<DirectorySummaryDto>>(
            cancellationToken: TestContext.Current.CancellationToken);
        body!.Items.Count.Should().Be(0);
    }

    [Fact]
    public async Task Unauthenticated_Returns401()
    {
        var response = await Anon.GetAsync(Route(Guid.NewGuid()), TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}