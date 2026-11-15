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

public class SearchFilesByTagsTests(AlexandriaFixture fixture) : FullStackTestBase(fixture)
{
    private const string Route = "/api/files/search/tags";

    [Fact]
    public async Task SecondPage_ReturnsRemainingFiles()
    {
        var tagId = await SeedTagAsync(UserId);

        for (var i = 0; i < 3; i++)
        {
            var file = await SeedFileWithVersionAsync();
            await TagFileAsync(file.Id, tagId);
        }

        var body = await SearchAsync(tagId, page: 1, pageSize: 2);

        body.TotalCount.Should().Be(3);
        body.Items.Should().HaveCount(1);
    }

    [Fact]
    public async Task OtherUsersFiles_AreNotReturned_EvenWithTheSameTag()
    {
        var (_, otherId) = await CreateOtherUserAsync();
        var tagId = await SeedTagAsync(UserId);

        var own = await SeedFileWithVersionAsync();
        var foreign = await SeedFileWithVersionAsync(fb => fb.WithOwner(otherId));
        await TagFileAsync(own.Id, tagId);
        await TagFileAsync(foreign.Id, tagId);

        var body = await SearchAsync(tagId, page: 0, pageSize: 10);

        body.Items.Should().ContainSingle(f => f.FileId == own.Id);
    }

    [Fact]
    public async Task MinFileSize_ExcludesSmallerFiles()
    {
        var tagId = await SeedTagAsync(UserId);

        var small = await SeedFileWithVersionAsync(configureVersion: vb => vb.WithSize(10));
        var large = await SeedFileWithVersionAsync(configureVersion: vb => vb.WithSize(10_000));
        await TagFileAsync(small.Id, tagId);
        await TagFileAsync(large.Id, tagId);

        var response = await Auth.GetAsync($"{Route}?tagIds={tagId}&minFileSize=1000&page=0&pageSize=10",
            cancellationToken: TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<PaginatedResult<FileResult>>(
            cancellationToken: TestContext.Current.CancellationToken);
        body!.Items.Should().ContainSingle(f => f.FileId == large.Id);
    }

    private async Task<PaginatedResult<FileResult>> SearchAsync(Guid tagId, int page, int pageSize)
    {
        var response = await Auth.GetAsync($"{Route}?tagIds={tagId}&page={page}&pageSize={pageSize}",
            cancellationToken: TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<PaginatedResult<FileResult>>(
            cancellationToken: TestContext.Current.CancellationToken);

        return body!;
    }

    private async Task<Guid> SeedTagAsync(Guid ownerId)
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AlexandriaDbContext>();

        var tag = new TagBuilder().WithOwner(ownerId).Build();
        db.Tags.Add(tag);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        return tag.Id;
    }

    private async Task TagFileAsync(Guid fileId, Guid tagId)
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AlexandriaDbContext>();

        db.FileTags.Add(new FileTag
        {
            Id = Guid.NewGuid(),
            FileId = fileId,
            TagId = tagId,
            Source = TagSource.User,
            CreatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
    }
}
