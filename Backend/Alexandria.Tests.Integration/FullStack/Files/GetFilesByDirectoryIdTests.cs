using System.Net;
using System.Net.Http.Json;
using Alexandria.Data.Models.Enumerators;
using Alexandria.Dto.Files;
using Alexandria.Tests.Common.Fixtures;
using AwesomeAssertions;
using Xunit;

namespace Alexandria.Tests.Integration.FullStack.Files;

public class GetFilesByDirectoryIdTests(AlexandriaFixture fixture) : FullStackTestBase(fixture)
{
    private static string Route(Guid directoryId) =>
        $"/api/files/directory/{directoryId}?page=1&pageSize=10&sortBy={SortBy.Name}&sortDirection={SortDirection.Asc}";

    [Fact]
    public async Task FilesInDirectory_AreReturned()
    {
        var dir = await SeedDirectoryAsync();
        await SeedFileWithVersionAsync(fb => fb.WithDirectory(dir.Id));
        await SeedFileWithVersionAsync(fb => fb.WithDirectory(dir.Id));

        var response = await Auth.GetAsync(Route(dir.Id), TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<PaginatedResult<FileResult>>(
            cancellationToken: TestContext.Current.CancellationToken);
        body!.TotalCount.Should().Be(2);
    }

    [Fact]
    public async Task FilesInOtherDirectory_AreNotReturned()
    {
        var dir1 = await SeedDirectoryAsync();
        var dir2 = await SeedDirectoryAsync();
        await SeedFileWithVersionAsync(fb => fb.WithDirectory(dir2.Id));

        var response = await Auth.GetAsync(Route(dir1.Id), TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<PaginatedResult<FileResult>>(
            cancellationToken: TestContext.Current.CancellationToken);
        body!.TotalCount.Should().Be(0);
    }

    [Fact]
    public async Task EmptyDirectory_ReturnsEmptyPage()
    {
        var dir = await SeedDirectoryAsync();

        var response = await Auth.GetAsync(Route(dir.Id), TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<PaginatedResult<FileResult>>(
            cancellationToken: TestContext.Current.CancellationToken);
        body!.Items.Count.Should().Be(0);
    }

    [Fact]
    public async Task Unauthenticated_Returns401()
    {
        var response = await Anon.GetAsync(Route(Guid.NewGuid()), TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Pagination_Works()
    {
        var dir = await SeedDirectoryAsync();
        for (var i = 0; i < 5; i++)
            await SeedFileWithVersionAsync(fb => fb.WithDirectory(dir.Id));

        var route =
            $"/api/files/directory/{dir.Id}?page=1&pageSize=2&sortBy={SortBy.Name}&sortDirection={SortDirection.Asc}";
        var response = await Auth.GetAsync(route, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<PaginatedResult<FileResult>>(
            cancellationToken: TestContext.Current.CancellationToken);
        body!.TotalCount.Should().Be(5);
        body.Items.Count.Should().Be(2);
    }
}