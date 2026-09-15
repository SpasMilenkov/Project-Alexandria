using Alexandria.Common.Policies;
using AwesomeAssertions;
using File = Alexandria.Data.Models.File;

namespace Alexandria.Tests.Unit.AutoTagging;

public class EnrichmentBackfillFilterTests
{
    private static readonly Guid RootDir = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid ChildDir = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid OtherDir = Guid.Parse("33333333-3333-3333-3333-333333333333");

    private static readonly DateTime Cutoff = new(2026, 8, 1, 12, 0, 0, DateTimeKind.Utc);

    private static File FileIn(Guid? directoryId, DateTime createdAt, bool deleted = false) => new()
    {
        Name = "track.mp3",
        MimeType = "audio/mpeg",
        DirectoryId = directoryId,
        CreatedAt = createdAt,
        DeletedAt = deleted ? createdAt.AddHours(1) : null,
    };

    [Fact]
    public void BuildScopeFilter_matches_live_predating_file_in_scope()
    {
        var filter = EnrichmentBackfill.BuildScopeFilter([RootDir, ChildDir], Cutoff).Compile();

        filter(FileIn(RootDir, Cutoff.AddHours(-1))).Should().BeTrue();
        filter(FileIn(ChildDir, Cutoff.AddDays(-30))).Should().BeTrue();
    }

    [Fact]
    public void BuildScopeFilter_excludes_out_of_scope_directory()
    {
        var filter = EnrichmentBackfill.BuildScopeFilter([RootDir], Cutoff).Compile();

        filter(FileIn(OtherDir, Cutoff.AddHours(-1))).Should().BeFalse();
        filter(FileIn(null, Cutoff.AddHours(-1))).Should().BeFalse();
    }

    [Fact]
    public void BuildScopeFilter_excludes_files_created_at_or_after_cutoff()
    {
        var filter = EnrichmentBackfill.BuildScopeFilter([RootDir], Cutoff).Compile();

        filter(FileIn(RootDir, Cutoff)).Should().BeFalse();
        filter(FileIn(RootDir, Cutoff.AddSeconds(1))).Should().BeFalse();
    }

    [Fact]
    public void BuildScopeFilter_excludes_deleted_files()
    {
        var filter = EnrichmentBackfill.BuildScopeFilter([RootDir], Cutoff).Compile();

        filter(FileIn(RootDir, Cutoff.AddHours(-1), deleted: true)).Should().BeFalse();
    }

    [Fact]
    public void RemoveAlreadyEnriched_drops_ready_ids_preserving_order()
    {
        var candidates = new[] { Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid() };

        var remaining = EnrichmentBackfill.RemoveAlreadyEnriched(
            candidates, new[] { candidates[1] });

        remaining.Should().BeEquivalentTo([candidates[0], candidates[2]]);
    }

    [Fact]
    public void RemoveAlreadyEnriched_empty_ready_keeps_all()
    {
        var candidates = new[] { Guid.NewGuid(), Guid.NewGuid() };

        EnrichmentBackfill.RemoveAlreadyEnriched(candidates, [])
            .Should().BeEquivalentTo(candidates);
    }
}