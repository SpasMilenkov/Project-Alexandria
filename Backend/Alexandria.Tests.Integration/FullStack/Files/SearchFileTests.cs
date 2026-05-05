using System.Net;
using System.Net.Http.Json;
using Alexandria.Dto.Files;
using Alexandria.Tests.Common.Fixtures;
using AwesomeAssertions;
using Xunit;

namespace Alexandria.Tests.Integration.FullStack.Files;

public class SearchFileTests(AlexandriaFixture fixture) : FullStackTestBase(fixture)
{
    private const string Route = "/api/files/search";

    [Fact]
    public async Task EmptyDb_ReturnsEmptyPage()
    {
        var response = await Auth.GetAsync($"{Route}?pageSize=10&currentPage=0",
            cancellationToken: TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<PaginatedResult<FileResult>>(
            cancellationToken: TestContext.Current.CancellationToken);
        body!.Items.Count.Should().Be(0);
    }

    [Fact]
    public async Task OwnFiles_AreReturned()
    {
        await SeedFileWithVersionAsync();
        await SeedFileWithVersionAsync();

        var response = await Auth.GetAsync($"{Route}?pageSize=10&currentPage=0",
            cancellationToken: TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<PaginatedResult<FileResult>>(
            cancellationToken: TestContext.Current.CancellationToken);
        body!.TotalCount.Should().Be(2);
    }

    [Fact]
    public async Task OtherUsersFiles_AreNotReturned()
    {
        var (_, otherId) = await CreateOtherUserAsync();
        await SeedFileWithVersionAsync(fb => fb.WithOwner(otherId));

        var response = await Auth.GetAsync($"{Route}?pageSize=10&currentPage=0",
            cancellationToken: TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<PaginatedResult<FileResult>>(
            cancellationToken: TestContext.Current.CancellationToken);
        body!.TotalCount.Should().Be(0);
    }

    [Fact]
    public async Task NameContains_FiltersCorrectly()
    {
        await SeedFileWithVersionAsync(fb => fb.WithName("report.txt"));
        await SeedFileWithVersionAsync(fb => fb.WithName("photo.jpg"));

        var response = await Auth.GetAsync($"{Route}?nameContains=report&pageSize=10&currentPage=0",
            cancellationToken: TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<PaginatedResult<FileResult>>(
            cancellationToken: TestContext.Current.CancellationToken);
        body!.TotalCount.Should().Be(1);
        body.Items.Should().ContainSingle(f => f.FileName == "report.txt");
    }

    [Fact]
    public async Task OnlyDeleted_ShowsDeletedFiles()
    {
        await SeedFileWithVersionAsync(fb => fb.WithDeletedAt(DateTime.UtcNow));
        await SeedFileWithVersionAsync(); // active file

        var activeResponse = await Auth.GetAsync($"{Route}?isDeleted=false&pageSize=10&currentPage=0",
            cancellationToken: TestContext.Current.CancellationToken);
        var activeBody =
            await activeResponse.Content.ReadFromJsonAsync<PaginatedResult<FileResult>>(
                cancellationToken: TestContext.Current.CancellationToken);
        activeBody!.TotalCount.Should().Be(1);

        var deletedResponse = await Auth.GetAsync($"{Route}?onlyDeleted=true&pageSize=10&currentPage=0",
            cancellationToken: TestContext.Current.CancellationToken);
        var deletedBody =
            await deletedResponse.Content.ReadFromJsonAsync<PaginatedResult<FileResult>>(
                cancellationToken: TestContext.Current.CancellationToken);
        deletedBody!.TotalCount.Should().Be(1);
    }

    [Fact]
    public async Task Unauthenticated_Returns401()
    {
        var response = await Anon.GetAsync($"{Route}?pageSize=10&currentPage=0",
            cancellationToken: TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Pagination_Works()
    {
        for (var i = 0; i < 5; i++)
            await SeedFileWithVersionAsync();

        var response = await Auth.GetAsync($"{Route}?pageSize=2&currentPage=0",
            cancellationToken: TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<PaginatedResult<FileResult>>(
            cancellationToken: TestContext.Current.CancellationToken);
        body!.TotalCount.Should().Be(5);
        body.Items.Count.Should().Be(2);
    }
}