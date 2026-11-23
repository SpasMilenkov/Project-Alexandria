using System.Net;
using System.Net.Http.Json;
using Alexandria.Common.Config;
using Alexandria.Data.Context;
using Alexandria.Data.Models;
using Alexandria.Data.Models.Enumerators;
using Alexandria.Dto.Links;
using Alexandria.Tests.Common.Builders;
using Alexandria.Tests.Common.Fixtures;
using AwesomeAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Alexandria.Tests.Integration.FullStack.Notes;

public class ResolveLinksTests(AlexandriaFixture fixture) : FullStackTestBase(fixture)
{
    private const string Route = "/api/links/resolve";

    private async Task<HttpResponseMessage> ResolveAsync(HttpClient client, params LinkTargetRef[] targets) =>
        await client.PostAsJsonAsync(Route, new { targets },
            cancellationToken: TestContext.Current.CancellationToken);

    private static async Task<IReadOnlyList<LinkTargetDto>> ReadTargetsAsync(HttpResponseMessage response)
    {
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<ResolveLinksResponse>(
            cancellationToken: TestContext.Current.CancellationToken);

        return body!.Targets;
    }

    private async Task<Guid> SeedTagAsync(Guid ownerId, string name, DateTime? deletedAt = null)
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AlexandriaDbContext>();

        var tag = new TagBuilder().WithOwner(ownerId).WithName(name).WithDeletedAt(deletedAt).Build();
        db.Tags.Add(tag);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        return tag.Id;
    }

    // System tags belong to the system account; the reset between tests may have removed it.
    private async Task EnsureSystemUserAsync()
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AlexandriaDbContext>();

        if (await db.Users.AnyAsync(u => u.Id == SystemConfig.SystemId, TestContext.Current.CancellationToken))
            return;

        db.Users.Add(new UserBuilder().WithId(SystemConfig.SystemId).Build());
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    private async Task<Guid> SeedPlaylistAsync(string name)
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AlexandriaDbContext>();

        var playlist = new Playlist { Name = name, OwnerId = UserId, CreatedAt = DateTime.UtcNow };
        db.Playlists.Add(playlist);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        return playlist.Id;
    }

    [Fact]
    public async Task Resolve_ownTargets_returnsLiveNamesInRequestOrder()
    {
        var folder = await SeedDirectoryAsync(configure: d => d.WithName("Paris"));
        var note = await SeedFileWithVersionAsync(
            f => f.WithName("Trip.md").WithMimeType("text/markdown").WithDirectory(folder.Id),
            v => v.WithMimeType("text/markdown"));
        var tagId = await SeedTagAsync(UserId, "Vacation");
        var playlistId = await SeedPlaylistAsync("Road trip");

        var targets = await ReadTargetsAsync(await ResolveAsync(Auth,
            new LinkTargetRef(LinkTargetKind.Playlist, playlistId),
            new LinkTargetRef(LinkTargetKind.File, note.Id),
            new LinkTargetRef(LinkTargetKind.Tag, tagId),
            new LinkTargetRef(LinkTargetKind.Folder, folder.Id)));

        targets.Select(t => t.Name).Should().Equal("Road trip", "Trip.md", "Vacation", "Paris");
        targets.Should().OnlyContain(t => t.State == LinkTargetState.Available);

        var file = targets[1];
        file.MimeType.Should().Be("text/markdown");
        file.DirectoryId.Should().Be(folder.Id);
        file.VersionId.Should().Be(note.CurrentVersionId);
        file.IsEncrypted.Should().BeFalse();
    }

    [Fact]
    public async Task Resolve_trashedFileAndFolder_areTrashed()
    {
        var folder = await SeedDirectoryAsync(configure: d => d.WithDeletedAt(DateTime.UtcNow));
        var file = await SeedFileWithVersionAsync(f => f.WithDeletedAt(DateTime.UtcNow));

        var targets = await ReadTargetsAsync(await ResolveAsync(Auth,
            new LinkTargetRef(LinkTargetKind.Folder, folder.Id),
            new LinkTargetRef(LinkTargetKind.File, file.Id)));

        targets.Should().OnlyContain(t => t.State == LinkTargetState.Trashed && t.Name != null);
    }

    [Fact]
    public async Task Resolve_anotherUsersTargets_lookTheSameAsMissingOnes()
    {
        var (_, otherId) = await CreateOtherUserAsync();
        var theirFile = await SeedFileWithVersionAsync(f => f.WithOwner(otherId).WithName("Secret.md"));
        var theirFolder = await SeedDirectoryAsync(configure: d => d.WithOwner(otherId).WithName("Private"));
        var theirTag = await SeedTagAsync(otherId, "Hidden");

        var targets = await ReadTargetsAsync(await ResolveAsync(Auth,
            new LinkTargetRef(LinkTargetKind.File, theirFile.Id),
            new LinkTargetRef(LinkTargetKind.Folder, theirFolder.Id),
            new LinkTargetRef(LinkTargetKind.Tag, theirTag),
            new LinkTargetRef(LinkTargetKind.File, Guid.NewGuid())));

        targets.Should().HaveCount(4);
        targets.Should().OnlyContain(t => t.State == LinkTargetState.Unavailable && t.Name == null);
    }

    [Fact]
    public async Task Resolve_systemTag_isAvailableToEveryone()
    {
        await EnsureSystemUserAsync();
        var systemTag = await SeedTagAsync(SystemConfig.SystemId, "Rock");

        var targets = await ReadTargetsAsync(await ResolveAsync(Auth,
            new LinkTargetRef(LinkTargetKind.Tag, systemTag)));

        targets.Should().ContainSingle().Which.Name.Should().Be("Rock");
    }

    [Fact]
    public async Task Resolve_deletedTag_isUnavailable()
    {
        var tagId = await SeedTagAsync(UserId, "Gone", DateTime.UtcNow);

        var targets = await ReadTargetsAsync(await ResolveAsync(Auth,
            new LinkTargetRef(LinkTargetKind.Tag, tagId)));

        targets.Should().ContainSingle().Which.State.Should().Be(LinkTargetState.Unavailable);
    }

    [Fact]
    public async Task Resolve_noTargets_returns400()
    {
        var response = await ResolveAsync(Auth);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Resolve_unauthenticated_returns401()
    {
        var response = await ResolveAsync(Anon, new LinkTargetRef(LinkTargetKind.File, Guid.NewGuid()));

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
