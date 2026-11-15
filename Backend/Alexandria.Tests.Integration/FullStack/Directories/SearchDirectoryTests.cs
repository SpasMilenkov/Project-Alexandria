using System.Net;
using System.Net.Http.Json;
using Alexandria.Dto.Directories;
using Alexandria.Dto.Files;
using Alexandria.Tests.Common.Fixtures;
using AwesomeAssertions;
using Xunit;

namespace Alexandria.Tests.Integration.FullStack.Directories;

public class SearchDirectoryTests(AlexandriaFixture fixture) : FullStackTestBase(fixture)
{
    private const string BaseRoute = "/api/directories/search";

    [Fact]
    public async Task NoDirectories_ReturnsEmpty()
    {
        var response = await Auth.GetAsync(BaseRoute, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<PaginatedResult<DirectorySummaryDto>>(
            cancellationToken: TestContext.Current.CancellationToken);
        body!.Items.Count.Should().Be(0);
    }

    [Fact]
    public async Task NameContains_FiltersCorrectly()
    {
        await SeedDirectoryAsync(configure: b => b.WithName("photos-2024"));
        await SeedDirectoryAsync(configure: b => b.WithName("documents"));

        var response = await Auth.GetAsync($"{BaseRoute}?nameContains=photos", TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<PaginatedResult<DirectorySummaryDto>>(
            cancellationToken: TestContext.Current.CancellationToken);
        body!.TotalCount.Should().Be(1);
        body.Items.Should().ContainSingle(d => d.Name == "photos-2024");
    }

    [Fact]
    public async Task OnlyOwnDirectories_AreReturned()
    {
        var (_, otherId) = await CreateOtherUserAsync();
        await SeedDirectoryAsync(); // own
        await SeedDirectoryAsync(configure: b => b.WithOwner(otherId)); // other user's

        var response = await Auth.GetAsync(BaseRoute, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<PaginatedResult<DirectorySummaryDto>>(
            cancellationToken: TestContext.Current.CancellationToken);
        body!.TotalCount.Should().Be(1);
    }

    [Fact]
    public async Task IsDeleted_ShowsDeletedDirectories()
    {
        await SeedDirectoryAsync(configure: b => b.WithDeletedAt(DateTime.UtcNow)); // deleted
        await SeedDirectoryAsync(); // active

        var activeResponse = await Auth.GetAsync($"{BaseRoute}?isDeleted=false", TestContext.Current.CancellationToken);
        var activeBody =
            await activeResponse.Content.ReadFromJsonAsync<PaginatedResult<DirectorySummaryDto>>(
                cancellationToken: TestContext.Current.CancellationToken);
        activeBody!.TotalCount.Should().Be(1);

        var deletedResponse = await Auth.GetAsync($"{BaseRoute}?isDeleted=true", TestContext.Current.CancellationToken);
        var deletedBody =
            await deletedResponse.Content.ReadFromJsonAsync<PaginatedResult<DirectorySummaryDto>>(
                cancellationToken: TestContext.Current.CancellationToken);
        deletedBody!.TotalCount.Should().Be(1);
    }

    [Fact]
    public async Task Unauthenticated_Returns401()
    {
        var response = await Anon.GetAsync(BaseRoute, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task DeletedAfter_FiltersByDeletionDate_NotCreationDate()
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var monthAgo = today.AddDays(-30).ToDateTime(new TimeOnly(12, 0), DateTimeKind.Utc);
        var startOfToday = today.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);

        await SeedDirectoryAsync(configure: b => b
            .WithName("old-but-just-deleted")
            .WithCreatedAt(monthAgo)
            .WithDeletedAt(startOfToday));
        await SeedDirectoryAsync(configure: b => b
            .WithName("new-but-deleted-long-ago")
            .WithCreatedAt(startOfToday)
            .WithDeletedAt(monthAgo));

        var response = await Auth.GetAsync(
            $"{BaseRoute}?isDeleted=true&deletedAfter={today.AddDays(-7):yyyy-MM-dd}",
            TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<PaginatedResult<DirectorySummaryDto>>(
            cancellationToken: TestContext.Current.CancellationToken);
        body!.Items.Should().ContainSingle(d => d.Name == "old-but-just-deleted");
    }

    [Fact]
    public async Task NameContains_RanksClosestNameFirst()
    {
        await SeedDirectoryAsync(configure: b => b.WithName("holiday photos archive"));
        await SeedDirectoryAsync(configure: b => b.WithName("photos"));

        var response = await Auth.GetAsync($"{BaseRoute}?nameContains=photos", TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<PaginatedResult<DirectorySummaryDto>>(
            cancellationToken: TestContext.Current.CancellationToken);
        body!.Items.Select(d => d.Name).Should().Equal("photos", "holiday photos archive");
    }
}
