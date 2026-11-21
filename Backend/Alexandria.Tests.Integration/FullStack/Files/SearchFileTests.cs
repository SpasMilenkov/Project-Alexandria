using System.Net;
using System.Net.Http.Json;
using Alexandria.Data.Context;
using Alexandria.Data.Models;
using Alexandria.Data.Models.Enumerators;
using Alexandria.Dto.Files;
using Alexandria.Tests.Common.Builders;
using Alexandria.Tests.Common.Fixtures;
using AwesomeAssertions;
using Microsoft.Extensions.DependencyInjection;
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
    public async Task NameContains_FindsATypoInsideALongName()
    {
        await SeedFileWithVersionAsync(fb =>
            fb.WithName("daniel-defoe_the-life-and-adventures-of-robinson-crusoe.epub"));

        var names = await SearchNamesAsync("robinsn", page: 0, pageSize: 10);

        names.Should().ContainSingle();
    }

    [Fact]
    public async Task NameContains_LeavesOutNamesThatOnlyShareAFewLetters()
    {
        // Both share trigrams with the query as a whole (0.18 and 0.14 similarity), but no word.
        await SeedFileWithVersionAsync(fb => fb.WithName("analysis_comparison.csv"));
        await SeedFileWithVersionAsync(fb => fb.WithName("Kecon-Gi.mp3"));

        (await SearchNamesAsync("paris", page: 0, pageSize: 10)).Should().BeEmpty();
        (await SearchNamesAsync("ML-KEM", page: 0, pageSize: 10)).Should().BeEmpty();
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

    [Fact]
    public async Task TagIds_ReturnOnlyFilesWithEveryTag()
    {
        var travel = await SeedTagAsync("travel");
        var family = await SeedTagAsync("family");

        var both = await SeedFileWithVersionAsync(fb => fb.WithName("paris.jpg"));
        var travelOnly = await SeedFileWithVersionAsync(fb => fb.WithName("rome.jpg"));
        await SeedFileWithVersionAsync(fb => fb.WithName("untagged.jpg"));
        await TagFileAsync(both.Id, travel, TagSource.User);
        await TagFileAsync(both.Id, family, TagSource.User);
        await TagFileAsync(travelOnly.Id, travel, TagSource.User);

        var oneTag = await SearchByTagsAsync(travel);
        var twoTags = await SearchByTagsAsync(travel, family);

        oneTag.Select(f => f.FileId).Should().BeEquivalentTo([both.Id, travelOnly.Id]);
        twoTags.Should().ContainSingle(f => f.FileId == both.Id);
    }

    [Fact]
    public async Task TagIds_IgnoreSuppressedAssignments()
    {
        var travel = await SeedTagAsync("travel");
        var file = await SeedFileWithVersionAsync();
        await TagFileAsync(file.Id, travel, TagSource.Suppressed);

        var results = await SearchByTagsAsync(travel);

        results.Should().BeEmpty();
    }

    [Fact]
    public async Task TagIds_EmptyGuid_Returns400()
    {
        var response = await Auth.GetAsync($"{Route}?tagIds={Guid.Empty}&pageSize=10&currentPage=0",
            cancellationToken: TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    private async Task<List<FileResult>> SearchByTagsAsync(params Guid[] tagIds)
    {
        var tagQuery = string.Join("&", tagIds.Select(id => $"tagIds={id}"));
        var response = await Auth.GetAsync($"{Route}?{tagQuery}&pageSize=10&currentPage=0",
            cancellationToken: TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<PaginatedResult<FileResult>>(
            cancellationToken: TestContext.Current.CancellationToken);

        return body!.Items.ToList();
    }

    private async Task<Guid> SeedTagAsync(string name)
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AlexandriaDbContext>();

        var tag = new TagBuilder().WithOwner(UserId).WithName(name).Build();
        db.Tags.Add(tag);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        return tag.Id;
    }

    private async Task TagFileAsync(Guid fileId, Guid tagId, TagSource source)
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AlexandriaDbContext>();

        db.FileTags.Add(new FileTag
        {
            Id = Guid.NewGuid(),
            FileId = fileId,
            TagId = tagId,
            Source = source,
            CreatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
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