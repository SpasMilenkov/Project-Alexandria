using System.Net;
using System.Net.Http.Json;
using Alexandria.Api.Features.Storage.Directories.CopyDirectory;
using Alexandria.Api.Features.Storage.Directories.MoveDir;
using Alexandria.Api.Features.Storage.Directories.RestoreDirectories;
using Alexandria.Api.Features.Storage.Directories.UpdateDir;
using Alexandria.Data.Context;
using Alexandria.Dto.Directories;
using Alexandria.Dto.Files;
using Alexandria.Tests.Common.Fixtures;
using AwesomeAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Alexandria.Tests.Integration.FullStack.Directories;

public class DirectoryFavoritesTests(AlexandriaFixture fixture) : FullStackTestBase(fixture)
{
    [Fact]
    public async Task SetFavorite_repeated_requests_preserve_state_and_metadata()
    {
        var updatedAt = new DateTime(2026, 10, 1, 12, 0, 0, DateTimeKind.Utc);
        var directory = await SeedDirectoryAsync(configure: builder => builder.WithUpdatedAt(updatedAt));

        foreach (var isStarred in new[] { true, true, false, false })
        {
            var response = await Auth.PatchAsJsonAsync($"/api/directories/{directory.Id}/favorite",
                new { isStarred }, TestContext.Current.CancellationToken);

            response.StatusCode.Should().Be(HttpStatusCode.OK);
            var body = await response.Content.ReadFromJsonAsync<DirectoryFavoriteResponse>(
                TestContext.Current.CancellationToken);
            body.Should().Be(new DirectoryFavoriteResponse(directory.Id, isStarred));

            using var scope = Factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AlexandriaDbContext>();
            var saved = await db.Directories.SingleAsync(d => d.Id == directory.Id,
                TestContext.Current.CancellationToken);

            saved.IsStarred.Should().Be(isStarred);
            saved.UpdatedAt.Should().Be(updatedAt);
            saved.Name.Should().Be(directory.Name);
        }
    }

    [Fact]
    public async Task SetFavorite_foreign_deleted_or_missing_directory_returns_not_found()
    {
        var (_, otherId) = await CreateOtherUserAsync();
        var foreign = await SeedDirectoryAsync(configure: builder => builder.WithOwner(otherId));
        var deleted = await SeedDirectoryAsync(configure: builder => builder.WithDeletedAt(DateTime.UtcNow));

        foreach (var id in new[] { foreign.Id, deleted.Id, Guid.NewGuid() })
        {
            var response = await Auth.PatchAsJsonAsync($"/api/directories/{id}/favorite",
                new { isStarred = true }, TestContext.Current.CancellationToken);

            response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        }

        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AlexandriaDbContext>();
        (await db.Directories.AnyAsync(d => d.IsStarred, TestContext.Current.CancellationToken))
            .Should().BeFalse();
    }

    [Fact]
    public async Task SetFavorite_missing_state_and_anonymous_requests_are_rejected()
    {
        var directory = await SeedDirectoryAsync();
        var route = $"/api/directories/{directory.Id}/favorite";

        var missing = await Auth.PatchAsJsonAsync(route, new { }, TestContext.Current.CancellationToken);
        var anonymous = await Anon.PatchAsJsonAsync(route, new { isStarred = true },
            TestContext.Current.CancellationToken);

        missing.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        anonymous.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Search_favorites_are_owner_scoped_and_exclude_trash()
    {
        var (_, otherId) = await CreateOtherUserAsync();
        var favorite = await SeedDirectoryAsync(configure: builder => builder.WithStarred());
        await SeedDirectoryAsync();
        await SeedDirectoryAsync(configure: builder => builder.WithStarred().WithOwner(otherId));
        await SeedDirectoryAsync(configure: builder => builder.WithStarred().WithDeletedAt(DateTime.UtcNow));

        var response = await Auth.GetAsync("/api/directories/search?isStarred=true",
            TestContext.Current.CancellationToken);
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<PaginatedResult<DirectorySummaryDto>>(
            TestContext.Current.CancellationToken);
        body!.TotalCount.Should().Be(1);
        body.Items.Should().ContainSingle(d => d.Id == favorite.Id && d.IsStarred);

        var unfiltered = await Auth.GetFromJsonAsync<PaginatedResult<DirectorySummaryDto>>(
            "/api/directories/search?isStarred=false", TestContext.Current.CancellationToken);
        unfiltered!.TotalCount.Should().Be(2);
    }

    [Fact]
    public async Task Listings_and_details_expose_the_favorite_flag()
    {
        var parent = await SeedDirectoryAsync(configure: builder => builder.WithStarred());
        var child = await SeedDirectoryAsync(parent.Id, builder => builder.WithStarred());

        var root = await Auth.GetFromJsonAsync<PaginatedResult<DirectorySummaryDto>>(
            "/api/directories/root?page=1&pageSize=20", TestContext.Current.CancellationToken);
        var children = await Auth.GetFromJsonAsync<PaginatedResult<DirectorySummaryDto>>(
            $"/api/directories/sub?directoryId={parent.Id}&page=1&pageSize=20", TestContext.Current.CancellationToken);
        var detail = await Auth.GetFromJsonAsync<Alexandria.Api.Features.Storage.Directories.GetDir.GetDirResult>(
            $"/api/directories?directoryId={parent.Id}", TestContext.Current.CancellationToken);

        root!.Items.Should().ContainSingle(d => d.Id == parent.Id && d.IsStarred);
        children!.Items.Should().ContainSingle(d => d.Id == child.Id && d.IsStarred);
        detail!.Directory.IsStarred.Should().BeTrue();
    }

    [Fact]
    public async Task Rename_move_and_restore_preserve_favorites()
    {
        var directory = await SeedDirectoryAsync(configure: builder => builder.WithStarred());
        var destination = await SeedDirectoryAsync();

        var rename = await Auth.PatchAsJsonAsync($"/api/directories/{directory.Id}",
            new UpdateDirRequest { Name = "favorite-renamed" }, TestContext.Current.CancellationToken);
        rename.StatusCode.Should().Be(HttpStatusCode.OK);
        var renamed = await rename.Content.ReadFromJsonAsync<UpdateDirResponse>(
            TestContext.Current.CancellationToken);
        renamed!.Directory.IsStarred.Should().BeTrue();

        var move = await Auth.PostAsJsonAsync("/api/directories/move",
            new MoveDirRequest { DirectoryIds = [directory.Id], DestinationId = destination.Id },
            TestContext.Current.CancellationToken);
        move.StatusCode.Should().Be(HttpStatusCode.OK);

        var delete = await Auth.DeleteAsync($"/api/directories/{directory.Id}",
            TestContext.Current.CancellationToken);
        delete.StatusCode.Should().Be(HttpStatusCode.OK);
        var trashed = await Auth.GetFromJsonAsync<PaginatedResult<DirectorySummaryDto>>(
            "/api/directories/search?isStarred=true", TestContext.Current.CancellationToken);
        trashed!.Items.Should().BeEmpty();

        var restore = await Auth.PostAsJsonAsync("/api/directories/restore",
            new RestoreDirectoriesRequest { DirectoryIds = [directory.Id] }, TestContext.Current.CancellationToken);
        restore.StatusCode.Should().Be(HttpStatusCode.OK);
        var restored = await Auth.GetFromJsonAsync<PaginatedResult<DirectorySummaryDto>>(
            "/api/directories/search?isStarred=true", TestContext.Current.CancellationToken);
        restored!.Items.Should().ContainSingle(d => d.Id == directory.Id && d.ParentId == destination.Id);
    }

    [Fact]
    public async Task Copy_starred_subtree_creates_unstarred_directories()
    {
        var directory = await SeedDirectoryAsync(configure: builder => builder.WithStarred());
        var child = await SeedDirectoryAsync(directory.Id, builder => builder.WithStarred());

        var response = await Auth.PostAsJsonAsync("/api/directories/copy",
            new CopyDirectoryRequest { DirectoryId = directory.Id }, TestContext.Current.CancellationToken);
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AlexandriaDbContext>();
        var copies = await db.Directories.Where(d => d.Id != directory.Id && d.Id != child.Id)
            .ToListAsync(TestContext.Current.CancellationToken);

        copies.Should().HaveCount(2);
        copies.Should().OnlyContain(d => !d.IsStarred);
    }
}
