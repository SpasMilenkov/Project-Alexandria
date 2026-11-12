using Alexandria.Repositories;
using Alexandria.Tests.Common.Builders;
using Alexandria.Tests.Common.Fixtures;
using AwesomeAssertions;
using Xunit;

namespace Alexandria.Tests.Integration.Audit;

/// <summary>
/// Lyrics audit rows are filed under the track's file; the lookup behind them is one projection
/// across transcode, version and file, which has to translate to a single SQL query.
/// </summary>
public class TrackFileLookupTests(DatabaseFixture fixture) : AuditTriggerTestBase(fixture)
{
    [Fact]
    public async Task GetTrackFile_returnsTheVersionAndFileBehindATranscode()
    {
        StreamableFileSeed seed;
        await using (var db = Fixture.CreateDbContext())
        {
            seed = await StreamingSeedHelper.SeedStreamableFileAsync(db, UserId, "song.flac", DateTime.UtcNow, 180,
                ct: Ct);
        }

        await using var read = Fixture.CreateDbContext();
        var track = await new TranspilationJobRepository(read).GetTrackFileAsync(seed.JobId, Ct);

        track.Should().NotBeNull();
        track!.VersionId.Should().Be(seed.VersionId);
        track.FileId.Should().Be(seed.FileId);
        track.FileName.Should().Be("song.flac");
    }

    [Fact]
    public async Task GetTrackFile_unknownTranscode_returnsNull()
    {
        await using var db = Fixture.CreateDbContext();

        var track = await new TranspilationJobRepository(db).GetTrackFileAsync(Guid.NewGuid(), Ct);

        track.Should().BeNull();
    }
}
