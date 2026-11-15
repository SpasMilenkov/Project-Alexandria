using System.Net;
using System.Net.Http.Json;
using Alexandria.Data.Context;
using Alexandria.Dto.Files;
using Alexandria.Dto.Tags;
using Alexandria.Tests.Common.Builders;
using Alexandria.Tests.Common.Fixtures;
using AwesomeAssertions;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Alexandria.Tests.Integration.FullStack.Tags;

public class SearchTagsTests(AlexandriaFixture fixture) : FullStackTestBase(fixture)
{
    private const string Route = "/api/tags/search";

    [Fact]
    public async Task NoPage_ReturnsFirstPage()
    {
        var tagId = await SeedTagAsync("travel");

        var body = await SearchAsync($"{Route}?nameContains=travel");

        body.Items.Should().ContainSingle(t => t.Id == tagId);
    }

    [Fact]
    public async Task SecondPage_ReturnsRemainingTags()
    {
        await SeedTagAsync("alpha");
        await SeedTagAsync("beta");
        await SeedTagAsync("gamma");

        var body = await SearchAsync($"{Route}?page=2&pageSize=2");

        body.TotalCount.Should().Be(3);
        body.Items.Should().HaveCount(1);
    }

    [Fact]
    public async Task PageZero_Returns400()
    {
        var response = await Auth.GetAsync($"{Route}?page=0",
            cancellationToken: TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    private async Task<PaginatedResult<TagDto>> SearchAsync(string url)
    {
        var response = await Auth.GetAsync(url, cancellationToken: TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<PaginatedResult<TagDto>>(
            cancellationToken: TestContext.Current.CancellationToken);

        return body!;
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
}
