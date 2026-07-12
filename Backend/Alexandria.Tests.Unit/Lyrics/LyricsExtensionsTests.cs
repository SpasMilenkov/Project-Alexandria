using Alexandria.Data.Models;
using Alexandria.Data.Models.Enumerators;
using Alexandria.Dto.Extensions;
using AwesomeAssertions;

namespace Alexandria.Tests.Unit.Lyrics;

public class LyricsExtensionsTests
{
    [Fact]
    public void ToDto_maps_all_fields()
    {
        var now = DateTime.UtcNow;
        var id = Guid.NewGuid();
        var entity = new TrackLyrics
        {
            Id = id,
            PlainLyrics = "plain text",
            SyncedLyrics = "[00:01.00] line",
            IsInstrumental = false,
            SourceProvider = LyricsProvider.LrclibPublic,
            Cached = true,
            ProviderTrackId = "42",
            ConfidenceScore = 10m,
            Status = LyricsStatus.Fetched,
            FetchedAt = now,
            TranspilationJobId = Guid.NewGuid()
        };

        var dto = entity.ToDto();

        dto.Id.Should().Be(id);
        dto.Status.Should().Be(LyricsStatus.Fetched);
        dto.Provider.Should().Be(LyricsProvider.LrclibPublic);
        dto.Cached.Should().BeTrue();
        dto.ConfidenceScore.Should().Be(10m);
        dto.FetchedAt.Should().Be(now);
        dto.PlayLyrics.Should().Be("plain text");
        dto.SyncedLyrics.Should().Be("[00:01.00] line");
    }

    [Fact]
    public void ToDto_handles_nullable_fields()
    {
        var entity = new TrackLyrics
        {
            Id = Guid.NewGuid(),
            SourceProvider = LyricsProvider.None,
            Status = LyricsStatus.PendingFetch,
            TranspilationJobId = Guid.NewGuid()
        };

        var dto = entity.ToDto();

        dto.ConfidenceScore.Should().BeNull();
        dto.FetchedAt.Should().BeNull();
        dto.PlayLyrics.Should().BeNull();
        dto.SyncedLyrics.Should().BeNull();
        dto.Provider.Should().Be(LyricsProvider.None);
        dto.Cached.Should().BeFalse();
    }
}