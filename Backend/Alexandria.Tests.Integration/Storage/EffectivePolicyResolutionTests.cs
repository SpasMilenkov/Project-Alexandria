using System.Text.Json;
using Alexandria.Data.Models;
using Alexandria.Data.Models.Enumerators;
using Alexandria.Repositories;
using Alexandria.Tests.Common.Builders;
using Alexandria.Tests.Common.Fixtures;
using AwesomeAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;
using Directory = Alexandria.Data.Models.Directory;

namespace Alexandria.Tests.Integration.Storage;

/// <summary>
/// The upward folder walk is a recursive query, so it runs against a real database rather than
/// an in-memory provider that cannot execute it.
/// </summary>
[Collection("Database")]
public class EffectivePolicyResolutionTests(DatabaseFixture fixture) : IAsyncLifetime
{
    private readonly Guid _userId = Guid.NewGuid();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync()
    {
        await using var db = fixture.CreateDbContext();
        db.Users.Add(new UserBuilder().WithId(_userId).Build());
        await db.SaveChangesAsync(Ct);
    }

    public async ValueTask DisposeAsync()
    {
        await fixture.ResetAsync();
    }

    [Fact]
    public async Task Resolve_deeplyNestedFolder_usesInheritedPolicyFromTheTop()
    {
        var music = await SeedDirectoryAsync("Music");
        var artist = await SeedDirectoryAsync("Artist", music.Id);
        var album = await SeedDirectoryAsync("Album", artist.Id);
        var policy = await SeedPolicyAsync(music.Id, inherited: true);

        var resolved = await ResolveAsync(album.Id);

        resolved!.Id.Should().Be(policy.Id);
    }

    [Fact]
    public async Task Resolve_nonInheritingPolicyHalfwayUp_doesNotBlockTheOneAbove()
    {
        var music = await SeedDirectoryAsync("Music");
        var artist = await SeedDirectoryAsync("Artist", music.Id);
        var album = await SeedDirectoryAsync("Album", artist.Id);
        var top = await SeedPolicyAsync(music.Id, inherited: true);
        await SeedPolicyAsync(artist.Id, inherited: false);

        var resolved = await ResolveAsync(album.Id);

        resolved!.Id.Should().Be(top.Id);
    }

    [Fact]
    public async Task Resolve_closerInheritedPolicy_winsOverTheTop()
    {
        var music = await SeedDirectoryAsync("Music");
        var artist = await SeedDirectoryAsync("Artist", music.Id);
        var album = await SeedDirectoryAsync("Album", artist.Id);
        await SeedPolicyAsync(music.Id, inherited: true);
        var closer = await SeedPolicyAsync(artist.Id, inherited: true);

        var resolved = await ResolveAsync(album.Id);

        resolved!.Id.Should().Be(closer.Id);
    }

    [Fact]
    public async Task Resolve_folderOwnPolicy_winsEvenWhenNotInherited()
    {
        var music = await SeedDirectoryAsync("Music");
        var artist = await SeedDirectoryAsync("Artist", music.Id);
        await SeedPolicyAsync(music.Id, inherited: true);
        var own = await SeedPolicyAsync(artist.Id, inherited: false);

        var resolved = await ResolveAsync(artist.Id);

        resolved!.Id.Should().Be(own.Id);
        resolved.Rules.Should().ContainSingle();
    }

    [Fact]
    public async Task Resolve_onlyNonInheritingAncestor_returnsNull()
    {
        var music = await SeedDirectoryAsync("Music");
        var artist = await SeedDirectoryAsync("Artist", music.Id);
        await SeedPolicyAsync(music.Id, inherited: false);

        (await ResolveAsync(artist.Id)).Should().BeNull();
    }

    [Fact]
    public async Task Resolve_folderOwnPolicyWithoutRules_fallsBackToTheInheritedOne()
    {
        var music = await SeedDirectoryAsync("Music");
        var album = await SeedDirectoryAsync("Album", music.Id);
        var top = await SeedPolicyAsync(music.Id, inherited: true);
        await SeedPolicyAsync(album.Id, inherited: false, ruleDeleted: true);

        var resolved = await ResolveAsync(album.Id);

        resolved!.Id.Should().Be(top.Id);
    }

    [Fact]
    public async Task Resolve_closerInheritedPolicyWithoutRules_doesNotBlockTheOneAbove()
    {
        var music = await SeedDirectoryAsync("Music");
        var artist = await SeedDirectoryAsync("Artist", music.Id);
        var album = await SeedDirectoryAsync("Album", artist.Id);
        var top = await SeedPolicyAsync(music.Id, inherited: true);
        await SeedPolicyAsync(artist.Id, inherited: true, ruleDeleted: true);

        var resolved = await ResolveAsync(album.Id);

        resolved!.Id.Should().Be(top.Id);
    }

    [Fact]
    public async Task Resolve_ruleDeletedInTheSameContext_stillFallsBack()
    {
        var music = await SeedDirectoryAsync("Music");
        var album = await SeedDirectoryAsync("Album", music.Id);
        var top = await SeedPolicyAsync(music.Id, inherited: true);
        var own = await SeedPolicyAsync(album.Id, inherited: false);

        await using var db = fixture.CreateDbContext();
        var tracked = await db.DirectoryPolicies.Include(p => p.Rules).SingleAsync(p => p.Id == own.Id, Ct);
        tracked.Rules.Single().DeletedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(Ct);

        var resolved = await new DirectoryPolicyRepository(db).ResolveEffectivePolicyAsync(album.Id, Ct);

        resolved!.Id.Should().Be(top.Id);
    }

    [Fact]
    public async Task ResolveAbove_ignoresTheFolderOwnPolicy_andSkipsNonInheritedAncestors()
    {
        var media = await SeedDirectoryAsync("Media");
        var music = await SeedDirectoryAsync("Music", media.Id);
        var album = await SeedDirectoryAsync("Album", music.Id);
        var top = await SeedPolicyAsync(media.Id, inherited: true);
        await SeedPolicyAsync(music.Id, inherited: false);
        await SeedPolicyAsync(album.Id, inherited: true);

        await using var db = fixture.CreateDbContext();
        var resolved = await new DirectoryPolicyRepository(db).ResolveInheritedPolicyAboveAsync(album.Id, Ct);

        resolved!.Id.Should().Be(top.Id);
    }

    private async Task<Alexandria.Dto.Policies.DirectoryPolicyDto?> ResolveAsync(Guid directoryId)
    {
        await using var db = fixture.CreateDbContext();

        return await new DirectoryPolicyRepository(db).ResolveEffectivePolicyAsync(directoryId, Ct);
    }

    private async Task<Directory> SeedDirectoryAsync(string name, Guid? parentId = null)
    {
        await using var db = fixture.CreateDbContext();
        var directory = new DirectoryBuilder().WithOwner(_userId).WithName(name).WithParent(parentId).Build();

        db.Directories.Add(directory);
        await db.SaveChangesAsync(Ct);

        return directory;
    }

    private async Task<DirectoryPolicy> SeedPolicyAsync(Guid directoryId, bool inherited, bool ruleDeleted = false)
    {
        DateTime? ruleDeletedAt = null;
        if (ruleDeleted) ruleDeletedAt = DateTime.UtcNow;

        await using var db = fixture.CreateDbContext();

        var policy = new DirectoryPolicy
        {
            Id = Guid.NewGuid(),
            DirectoryId = directoryId,
            InheritedByChildren = inherited,
            CreatedAt = DateTime.UtcNow,
        };

        db.DirectoryPolicies.Add(policy);
        db.PolicyRules.Add(new PolicyRule
        {
            Id = Guid.NewGuid(),
            PolicyId = policy.Id,
            ActionType = PolicyActionType.Transcode,
            TriggerType = PolicyTriggerType.FileGroup,
            TriggerValue = "Audio",
            Priority = 10,
            Parameters = JsonDocument.Parse("""{"audioRungs":[2],"videoRungs":[],"generateThumbnail":true}"""),
            CreatedAt = DateTime.UtcNow,
            DeletedAt = ruleDeletedAt,
        });

        await db.SaveChangesAsync(Ct);

        return policy;
    }
}
