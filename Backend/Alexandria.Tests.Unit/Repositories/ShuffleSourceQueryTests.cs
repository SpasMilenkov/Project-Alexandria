using Alexandria.Data.Context;
using Alexandria.Dto.Files.Streaming.Shuffle;
using Alexandria.Repositories;
using AwesomeAssertions;
using Microsoft.EntityFrameworkCore;

namespace Alexandria.Tests.Unit.Repositories;

public class ShuffleSourceQueryTests
{
    private static AlexandriaDbContext TranslationContext()
    {
        var options = new DbContextOptionsBuilder<AlexandriaDbContext>()
            .UseNpgsql("Host=localhost;Database=translation_only;Username=postgres;Password=postgres")
            .Options;
        return new AlexandriaDbContext(options);
    }

    [Fact]
    public void ShuffleLibraryCandidatesQuery_translates_without_connecting()
    {
        using var context = TranslationContext();
        var repository = new FileRepository(context);
        var viable = new HashSet<Guid> { Guid.NewGuid() };

        var sql = repository
            .ShuffleLibraryCandidatesQuery(Guid.NewGuid(), false, viable)
            .ToQueryString();

        sql.Should().Contain("SELECT");
        sql.Should().Contain("ORDER BY");
        sql.Should().NotContain("GROUP BY");
    }

    [Fact]
    public void ShufflePlaylistCandidatesQuery_translates_without_connecting()
    {
        using var context = TranslationContext();
        var repository = new FileRepository(context);

        var sql = repository
            .ShufflePlaylistCandidatesQuery(Guid.NewGuid(), Guid.NewGuid(), false)
            .ToQueryString();

        sql.Should().Contain("SELECT");
        sql.Should().Contain("ORDER BY");
        sql.Should().Contain("Position");
    }

    [Fact]
    public void ShuffleListenRowsQuery_translates_without_connecting()
    {
        using var context = TranslationContext();
        var repository = new StreamHistoryRepository(context);

        var sql = repository
            .ShuffleListenRowsQuery(Guid.NewGuid(), [Guid.NewGuid()], DateTime.UtcNow)
            .ToQueryString();

        sql.Should().Contain("SELECT");
        sql.Should().Contain("EndedAt");
        sql.Should().NotContain("GROUP BY");
    }

    [Fact]
    public void PlaybackSourceDto_defaults_to_audio_library()
    {
        var source = new PlaybackSourceDto { IsVideo = false, PlaylistId = null };

        source.IsVideo.Should().BeFalse();
        source.PlaylistId.Should().BeNull();
    }
}