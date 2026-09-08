using Alexandria.Dto.Files;
using Alexandria.Services.Storage.MediaMetadata;
using AwesomeAssertions;
using MediaMetadataEntity = Alexandria.Data.Models.MediaMetadata;

namespace Alexandria.Tests.Unit.S3Service;

public class MediaMetadataMergeTests
{
    private static MediaMetadataEntity Existing(
        string? title = null,
        string? artist = null,
        string? album = null,
        string? year = null,
        string? genre = null) => new()
    {
        Title = title,
        Artist = artist,
        Album = album,
        Year = year,
        Genre = genre,
    };

    private static MediaMetadataDto Incoming(
        string? title = "Incoming Title",
        string? artist = "Incoming Artist",
        string? album = "Incoming Album",
        string? year = "2024",
        string? genre = "Incoming Genre") => new()
    {
        Title = title,
        Artist = artist,
        Album = album,
        Year = year,
        Genre = genre,
    };

    [Fact]
    public void ApplyDescriptiveFields_nonEmptyExisting_preservesAll()
    {
        var existing = Existing("T", "A", "Al", "2020", "G");

        MediaMetadataMerge.ApplyDescriptiveFields(existing, Incoming());

        existing.Title.Should().Be("T");
        existing.Artist.Should().Be("A");
        existing.Album.Should().Be("Al");
        existing.Year.Should().Be("2020");
        existing.Genre.Should().Be("G");
    }

    [Fact]
    public void ApplyDescriptiveFields_emptyExisting_takesIncoming()
    {
        var existing = Existing();
        var incoming = Incoming();

        MediaMetadataMerge.ApplyDescriptiveFields(existing, incoming);

        existing.Title.Should().Be(incoming.Title);
        existing.Artist.Should().Be(incoming.Artist);
        existing.Album.Should().Be(incoming.Album);
        existing.Year.Should().Be(incoming.Year);
        existing.Genre.Should().Be(incoming.Genre);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ApplyDescriptiveFields_blankExisting_countsAsEmpty(string? blank)
    {
        var existing = Existing(title: blank, artist: blank, album: blank, year: blank, genre: blank);
        var incoming = Incoming();

        MediaMetadataMerge.ApplyDescriptiveFields(existing, incoming);

        existing.Title.Should().Be(incoming.Title);
        existing.Artist.Should().Be(incoming.Artist);
        existing.Album.Should().Be(incoming.Album);
        existing.Year.Should().Be(incoming.Year);
        existing.Genre.Should().Be(incoming.Genre);
    }

    [Fact]
    public void ApplyDescriptiveFields_partialExisting_mergesPerField()
    {
        var existing = Existing(title: "User Title", album: "User Album");
        var incoming = Incoming();

        MediaMetadataMerge.ApplyDescriptiveFields(existing, incoming);

        existing.Title.Should().Be("User Title");
        existing.Album.Should().Be("User Album");
        existing.Artist.Should().Be(incoming.Artist);
        existing.Year.Should().Be(incoming.Year);
        existing.Genre.Should().Be(incoming.Genre);
    }

    [Fact]
    public void ApplyDescriptiveFields_nullIncoming_leavesEmptyUnchanged()
    {
        var existing = Existing();
        var incoming = Incoming(title: null, artist: null, album: null, year: null, genre: null);

        MediaMetadataMerge.ApplyDescriptiveFields(existing, incoming);

        existing.Title.Should().BeNull();
        existing.Artist.Should().BeNull();
        existing.Album.Should().BeNull();
        existing.Year.Should().BeNull();
        existing.Genre.Should().BeNull();
    }

    [Fact]
    public void ApplyDescriptiveFields_allowOverwrite_replacesNonEmpty()
    {
        var existing = Existing("T", "A", "Al", "2020", "G");
        var incoming = Incoming();

        MediaMetadataMerge.ApplyDescriptiveFields(existing, incoming, allowOverwrite: true);

        existing.Title.Should().Be(incoming.Title);
        existing.Artist.Should().Be(incoming.Artist);
        existing.Album.Should().Be(incoming.Album);
        existing.Year.Should().Be(incoming.Year);
        existing.Genre.Should().Be(incoming.Genre);
    }

    [Fact]
    public void ApplyDescriptiveFields_allowOverwriteFalse_isDefault()
    {
        var existing = Existing("T", "A", "Al", "2020", "G");

        MediaMetadataMerge.ApplyDescriptiveFields(existing, Incoming());

        existing.Title.Should().Be("T");
        existing.Album.Should().Be("Al");
    }
}