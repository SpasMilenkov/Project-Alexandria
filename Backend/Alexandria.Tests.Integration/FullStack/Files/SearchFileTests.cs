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
    public async Task IsDeleted_ShowsDeletedFiles()
    {
        await SeedFileWithVersionAsync(fb => fb.WithDeletedAt(DateTime.UtcNow));
        await SeedFileWithVersionAsync(); // active file

        var activeResponse = await Auth.GetAsync($"{Route}?isDeleted=false&pageSize=10&currentPage=0",
            cancellationToken: TestContext.Current.CancellationToken);
        var activeBody =
            await activeResponse.Content.ReadFromJsonAsync<PaginatedResult<FileResult>>(
                cancellationToken: TestContext.Current.CancellationToken);
        activeBody!.TotalCount.Should().Be(1);

        var deletedResponse = await Auth.GetAsync($"{Route}?isDeleted=true&pageSize=10&currentPage=0",
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

    [Fact]
    public async Task DeletedAfter_FiltersByDeletionDate_NotCreationDate()
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var monthAgo = today.AddDays(-30).ToDateTime(new TimeOnly(12, 0), DateTimeKind.Utc);
        var startOfToday = today.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);

        await SeedFileWithVersionAsync(fb => fb
            .WithName("old-but-just-deleted.txt")
            .WithCreatedAt(monthAgo)
            .WithDeletedAt(startOfToday));
        await SeedFileWithVersionAsync(fb => fb
            .WithName("new-but-deleted-long-ago.txt")
            .WithCreatedAt(startOfToday)
            .WithDeletedAt(monthAgo));

        var response = await Auth.GetAsync(
            $"{Route}?isDeleted=true&deletedAfter={today.AddDays(-7):yyyy-MM-dd}&pageSize=10&currentPage=0",
            cancellationToken: TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<PaginatedResult<FileResult>>(
            cancellationToken: TestContext.Current.CancellationToken);
        body!.Items.Should().ContainSingle(f => f.FileName == "old-but-just-deleted.txt");
    }

    [Fact]
    public async Task NameContains_RanksClosestNameFirst_AcrossPages()
    {
        await SeedFileWithVersionAsync(fb => fb.WithName("annual report summary.txt"));
        await SeedFileWithVersionAsync(fb => fb.WithName("report.txt"));
        await SeedFileWithVersionAsync(fb => fb.WithName("reporting.txt"));

        var firstPage = await SearchNamesAsync("report", page: 0, pageSize: 1);
        var secondPage = await SearchNamesAsync("report", page: 1, pageSize: 1);
        var thirdPage = await SearchNamesAsync("report", page: 2, pageSize: 1);

        firstPage.Should().Equal("report.txt");
        firstPage.Concat(secondPage).Concat(thirdPage).Should().OnlyHaveUniqueItems().And.HaveCount(3);
    }

    [Fact]
    public async Task NameContains_PunctuationOnly_ReturnsEmptyPage()
    {
        await SeedFileWithVersionAsync(fb => fb.WithName("report.txt"));

        var names = await SearchNamesAsync("--", page: 0, pageSize: 10);

        names.Should().BeEmpty();
    }

    [Fact]
    public async Task UpdatedAfter_EarliestDay_IsAccepted()
    {
        await SeedFileWithVersionAsync(fb => fb.WithUpdatedAt(DateTime.UtcNow));
        await SeedFileWithVersionAsync(); // never updated

        var response = await Auth.GetAsync(
            $"{Route}?updatedAfter=0001-01-01&sortBy=2&sortDirection=1&pageSize=10&currentPage=0",
            cancellationToken: TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<PaginatedResult<FileResult>>(
            cancellationToken: TestContext.Current.CancellationToken);
        body!.TotalCount.Should().Be(1);
    }

    private async Task<List<string>> SearchNamesAsync(string text, int page, int pageSize)
    {
        var response = await Auth.GetAsync(
            $"{Route}?nameContains={Uri.EscapeDataString(text)}&pageSize={pageSize}&currentPage={page}",
            cancellationToken: TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<PaginatedResult<FileResult>>(
            cancellationToken: TestContext.Current.CancellationToken);

        return body!.Items.Select(f => f.FileName).ToList();
    }
}
