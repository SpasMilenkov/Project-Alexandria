using Alexandria.Common.Repositories;
using Alexandria.Data.Models.Enumerators;
using Alexandria.Dto.Files.Streaming.Playlist;
using Alexandria.Services.Streaming;
using AwesomeAssertions;
using NSubstitute;

namespace Alexandria.Tests.Unit.Streaming;

public class AutoPlaylistGroupingTests
{
    private static readonly Guid FileA = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid FileB = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid FileC = Guid.Parse("33333333-3333-3333-3333-333333333333");
    private static readonly Guid TagOne = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid TagTwo = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
    private static readonly Guid Owner = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc");

    private static AutoPlaylistGroupingRow Row(
        Guid fileId,
        string? artist = null,
        string? album = null,
        string? genre = null,
        string? year = null,
        params GroupingTagRef[] tags) =>
        new(fileId, artist, album, genre, year, tags);

    private static GroupingTagRef ActiveTag(
        Guid tagId,
        TagFacet? facet = null,
        Guid? parentTagId = null,
        string? parentName = null) =>
        new(tagId, TagSource.Auto, facet, false, parentTagId, parentName);

    [Fact]
    public void GroupArtists_mergesCaseAndWhitespaceVariants()
    {
        var rows = new[]
        {
            Row(FileA, artist: "  The Beatles"),
            Row(FileB, artist: "the beatles "),
            Row(FileC, artist: "THE BEATLES"),
        };

        var groups = AutoPlaylistGrouping.GroupArtists(rows);

        var group = groups.Should().ContainSingle().Subject;
        group.Key.Should().Be("the beatles");
        group.DisplayName.Should().Be("The Beatles");
        group.FileIds.Should().HaveCount(3);
    }

    [Fact]
    public void GroupArtists_blankArtist_skipsRow()
    {
        var rows = new[]
        {
            Row(FileA, artist: null),
            Row(FileB, artist: ""),
            Row(FileC, artist: "   "),
        };

        AutoPlaylistGrouping.GroupArtists(rows).Should().BeEmpty();
    }

    [Fact]
    public void GroupAlbums_sameAlbumDifferentArtists_producesSeparateBuckets()
    {
        var rows = new[]
        {
            Row(FileA, artist: "Artist A", album: "Greatest Hits"),
            Row(FileB, artist: "Artist B", album: "Greatest Hits"),
        };

        var groups = AutoPlaylistGrouping.GroupAlbums(rows);

        groups.Should().HaveCount(2);
        groups.Select(g => g.ArtistKey).Should().BeEquivalentTo("artist a", "artist b");
        groups.Should().OnlyContain(g => g.AlbumKey == "greatest hits");
    }

    [Fact]
    public void GroupAlbums_mergesCompositeCaseVariants()
    {
        var rows = new[]
        {
            Row(FileA, artist: "Arcane", album: "  League of Legends "),
            Row(FileB, artist: "ARCANE", album: "league of legends"),
        };

        var groups = AutoPlaylistGrouping.GroupAlbums(rows);

        var group = groups.Should().ContainSingle().Subject;
        group.ArtistKey.Should().Be("arcane");
        group.AlbumKey.Should().Be("league of legends");
        group.DisplayArtist.Should().Be("Arcane");
        group.DisplayAlbum.Should().Be("League of Legends");
        group.FileIds.Should().HaveCount(2);
    }

    [Fact]
    public void GroupAlbums_albumWithoutArtist_skipsRow()
    {
        var rows = new[]
        {
            Row(FileA, artist: null, album: "Orphan Album"),
            Row(FileB, artist: "   ", album: "Orphan Album"),
        };

        AutoPlaylistGrouping.GroupAlbums(rows).Should().BeEmpty();
    }

    [Fact]
    public void GroupTags_excludesSuppressedAndDeletedTags()
    {
        var rows = new[]
        {
            Row(FileA, tags:
            [
                new GroupingTagRef(TagOne, TagSource.Auto, null, false, null, null),
                new GroupingTagRef(TagOne, TagSource.Suppressed, null, false, null, null),
                new GroupingTagRef(TagTwo, TagSource.User, null, true, null, null),
            ]),
        };

        var groups = AutoPlaylistGrouping.GroupTags(rows);

        var group = groups.Should().ContainSingle().Subject;
        group.TagId.Should().Be(TagOne);
        group.FileIds.Should().ContainSingle().Subject.Should().Be(FileA);
    }

    [Fact]
    public void GroupTags_carriesFirstSeenFacet()
    {
        var rows = new[]
        {
            Row(FileA, tags: ActiveTag(TagOne, TagFacet.Genre)),
            Row(FileB, tags: ActiveTag(TagOne, TagFacet.Genre)),
        };

        var groups = AutoPlaylistGrouping.GroupTags(rows);

        var group = groups.Should().ContainSingle().Subject;
        group.Facet.Should().Be(TagFacet.Genre);
        group.FileIds.Should().HaveCount(2);
    }

    [Fact]
    public void GroupTags_filesWithoutTags_appearNowhere()
    {
        var rows = new[]
        {
            Row(FileA),
            Row(FileB, tags: ActiveTag(TagOne)),
        };

        var groups = AutoPlaylistGrouping.GroupTags(rows);

        var group = groups.Should().ContainSingle().Subject;
        group.FileIds.Should().ContainSingle().Subject.Should().Be(FileB);
    }

    [Fact]
    public void GroupGenreFallback_onlyFilesWithoutActiveTags()
    {
        var rows = new[]
        {
            Row(FileA, genre: "Rock"),
            Row(FileB, genre: "Rock", tags: ActiveTag(TagOne)),
            Row(FileC, genre: "Rock", tags:
            [
                new GroupingTagRef(TagTwo, TagSource.Suppressed, null, false, null, null),
            ]),
        };

        var groups = AutoPlaylistGrouping.GroupGenreFallback(rows);

        var group = groups.Should().ContainSingle().Subject;
        group.Key.Should().Be("rock");
        group.DisplayName.Should().Be("Rock");
        group.FileIds.Should().BeEquivalentTo([FileA, FileC]);
    }

    [Fact]
    public void GroupGenreFallback_mergesCaseVariants()
    {
        var rows = new[]
        {
            Row(FileA, genre: "Nu Metal"),
            Row(FileB, genre: "  NU METAL "),
        };

        var groups = AutoPlaylistGrouping.GroupGenreFallback(rows);

        groups.Should().ContainSingle().Subject.FileIds.Should().HaveCount(2);
    }

    [Fact]
    public void GroupAll_emptyInput_returnsEmptyResult()
    {
        var result = AutoPlaylistGrouping.GroupAll([]);

        result.Artists.Should().BeEmpty();
        result.Albums.Should().BeEmpty();
        result.Tags.Should().BeEmpty();
        result.GenreFallback.Should().BeEmpty();
    }

    [Fact]
    public void GroupAll_fileAppearsInEveryMatchingBucket()
    {
        var rows = new[]
        {
            Row(FileA, artist: "Arcane", album: "LoL", genre: "Rock", tags: ActiveTag(TagOne)),
        };

        var result = AutoPlaylistGrouping.GroupAll(rows);

        result.Artists.Should().ContainSingle().Subject.FileIds.Should().Contain(FileA);
        result.Albums.Should().ContainSingle().Subject.FileIds.Should().Contain(FileA);
        result.Tags.Should().ContainSingle().Subject.FileIds.Should().Contain(FileA);
        result.GenreFallback.Should().BeEmpty();
    }

    [Fact]
    public async Task GetGroupingAsync_passesOwnerToRepository()
    {
        var files = Substitute.For<IFileRepository>();
        var rows = new List<AutoPlaylistGroupingRow>
        {
            Row(FileA, artist: "Arcane"),
        };
        files.GetAutoPlaylistGroupingRowsAsync(Owner, Arg.Any<CancellationToken>())
            .Returns(rows);
        var sut = new AutoPlaylistGroupingService(files);

        var result = await sut.GetGroupingAsync(Owner, TestContext.Current.CancellationToken);

        await files.Received(1).GetAutoPlaylistGroupingRowsAsync(Owner, Arg.Any<CancellationToken>());
        result.Artists.Should().ContainSingle().Subject.Key.Should().Be("arcane");
    }

    [Fact]
    public void GroupParentGenres_unions_children_under_parent()
    {
        var parent = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var rows = new[]
        {
            Row(FileA, tags: ActiveTag(TagOne, TagFacet.Genre, parent, "Rock")),
            Row(FileB, tags: ActiveTag(TagTwo, TagFacet.Genre, parent, "Rock")),
            Row(FileC, tags: ActiveTag(Guid.NewGuid(), TagFacet.Mood)),
        };

        var groups = AutoPlaylistGrouping.GroupParentGenres(rows);

        var group = groups.Should().ContainSingle().Subject;
        group.ParentTagId.Should().Be(parent);
        group.DisplayName.Should().Be("Rock");
        group.FileIds.Should().BeEquivalentTo([FileA, FileB]);
    }

    [Fact]
    public void GroupParentGenres_direct_parent_tag_uses_own_id()
    {
        var parent = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var rows = new[]
        {
            Row(FileA, tags: ActiveTag(parent, TagFacet.Genre)),
        };

        var groups = AutoPlaylistGrouping.GroupParentGenres(rows);

        var group = groups.Should().ContainSingle().Subject;
        group.ParentTagId.Should().Be(parent);
        group.DisplayName.Should().BeEmpty();
    }

    [Fact]
    public void GroupParentGenres_dedupes_file_with_two_children_of_same_parent()
    {
        var parent = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var rows = new[]
        {
            Row(FileA, tags:
            [
                ActiveTag(TagOne, TagFacet.Genre, parent, "Rock"),
                ActiveTag(TagTwo, TagFacet.Genre, parent, "Rock"),
            ]),
        };

        var groups = AutoPlaylistGrouping.GroupParentGenres(rows);

        groups.Should().ContainSingle().Subject.FileIds.Should().ContainSingle();
    }

    [Fact]
    public void GroupDecades_buckets_strict_calendar_decades()
    {
        var rows = new[]
        {
            Row(FileA, year: "1999"),
            Row(FileB, year: "1990"),
            Row(FileC, year: "2010"),
        };

        var groups = AutoPlaylistGrouping.GroupDecades(rows);

        groups.Should().HaveCount(2);
        groups.Should().ContainSingle(g => g.Decade == 1990)
            .Subject.FileIds.Should().BeEquivalentTo([FileA, FileB]);
        groups.Should().ContainSingle(g => g.Decade == 2010);
    }

    [Fact]
    public void GroupDecades_skips_missing_and_out_of_range_years()
    {
        var rows = new[]
        {
            Row(FileA, year: null),
            Row(FileB, year: "2221"),
            Row(FileC, year: "99"),
            Row(FileA, year: "2005"),
        };

        var groups = AutoPlaylistGrouping.GroupDecades(rows);

        groups.Should().ContainSingle().Subject.Decade.Should().Be(2000);
    }
}