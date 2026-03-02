using System.Net;
using System.Net.Http.Json;
using API.Features.Storage.Directories.SearchDirectory;
using AwesomeAssertions;
using DTO.Directories;
using DTO.Files;
using Test.Common.Fixtures;
using Xunit;

namespace Test.Integration.FullStack.Directories;

public class SearchDirectoryTests(AlexandriaFixture fixture) : FullStackTestBase(fixture)
{
    private const string BaseRoute = "/api/directories/search";

    [Fact]
    public async Task NoDirectories_ReturnsEmpty()
    {
        var response = await Auth.GetAsync(BaseRoute, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<PaginatedResult<DirectorySummaryDto>>(cancellationToken: TestContext.Current.CancellationToken);
        body!.Items.Count.Should().Be(0);
    }

    [Fact]
    public async Task NameContains_FiltersCorrectly()
    {
        await SeedDirectoryAsync(configure: b => b.WithName("photos-2024"));
        await SeedDirectoryAsync(configure: b => b.WithName("documents"));

        var response = await Auth.GetAsync($"{BaseRoute}?nameContains=photos", TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<PaginatedResult<DirectorySummaryDto>>(cancellationToken: TestContext.Current.CancellationToken);
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
        var body = await response.Content.ReadFromJsonAsync<PaginatedResult<DirectorySummaryDto>>(cancellationToken: TestContext.Current.CancellationToken);
        body!.TotalCount.Should().Be(1);
    }

    [Fact]
    public async Task IsDeleted_ShowsDeletedDirectories()
    {
        await SeedDirectoryAsync(configure: b => b.WithDeletedAt(DateTime.UtcNow)); // deleted
        await SeedDirectoryAsync(); // active

        var activeResponse = await Auth.GetAsync($"{BaseRoute}?isDeleted=false", TestContext.Current.CancellationToken);
        var activeBody = await activeResponse.Content.ReadFromJsonAsync<PaginatedResult<DirectorySummaryDto>>(cancellationToken: TestContext.Current.CancellationToken);
        activeBody!.TotalCount.Should().Be(1);

        var deletedResponse = await Auth.GetAsync($"{BaseRoute}?isDeleted=true", TestContext.Current.CancellationToken);
        var deletedBody = await deletedResponse.Content.ReadFromJsonAsync<PaginatedResult<DirectorySummaryDto>>(cancellationToken: TestContext.Current.CancellationToken);
        deletedBody!.TotalCount.Should().Be(1);
    }

    [Fact]
    public async Task Unauthenticated_Returns401()
    {
        var response = await Anon.GetAsync(BaseRoute, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
