using Alexandria.Data.Context;
using Alexandria.Data.Models;
using Alexandria.Data.Models.Enumerators;
using Alexandria.Repositories;
using AwesomeAssertions;
using Microsoft.EntityFrameworkCore;

namespace Alexandria.Tests.Unit.Repositories;

public class PreviewRepositoryRemoveTests
{
    private static AlexandriaDbContext CreateContext()
    {
        // Never connects: the tracking conflict fires in the state manager,
        // before any database access.
        var options = new DbContextOptionsBuilder<AlexandriaDbContext>()
            .UseNpgsql("Host=localhost;Database=alexandria_unit_tests;Username=postgres;Password=postgres")
            .Options;
        return new AlexandriaDbContext(options);
    }

    private static FileVersion VersionInstance(Guid id) => new()
    {
        Id = id,
        ContentHash = [0xAB],
        Size = 1,
        VersionNumber = 1,
        MimeType = "image/png",
        CreatedBy = Guid.NewGuid(),
        ContentObjectId = Guid.NewGuid(),
        FileId = Guid.NewGuid()
    };

    private static Preview PreviewRow(Guid versionId, FileVersion version) => new()
    {
        Id = Guid.NewGuid(),
        MimeType = "image/png",
        Size = 10,
        Kind = PreviewKind.Preview,
        VersionId = versionId,
        Version = version,
        ObjectKey = "previews/ab12"
    };

    [Fact]
    public void remove_preview_with_populated_version_does_not_conflict_with_tracked_version()
    {
        using var context = CreateContext();
        var sut = new PreviewRepository(context);

        var versionId = Guid.NewGuid();
        context.Attach(VersionInstance(versionId));

        // GetByFileAsync is AsNoTracking with Include(p => p.Version), so each
        // row carries its own FileVersion instance for the same key.
        var preview = PreviewRow(versionId, VersionInstance(versionId));

        var act = () => sut.Remove(preview);

        act.Should().NotThrow();
        context.Entry(preview).State.Should().Be(EntityState.Deleted);
    }

    [Fact]
    public void remove_two_previews_sharing_one_version_deletes_both_rows()
    {
        using var context = CreateContext();
        var sut = new PreviewRepository(context);

        var versionId = Guid.NewGuid();
        var first = PreviewRow(versionId, VersionInstance(versionId));
        var second = PreviewRow(versionId, VersionInstance(versionId));

        var act = () =>
        {
            sut.Remove(first);
            sut.Remove(second);
        };

        act.Should().NotThrow();
        context.Entry(first).State.Should().Be(EntityState.Deleted);
        context.Entry(second).State.Should().Be(EntityState.Deleted);
    }

    [Fact]
    public void remove_range_with_populated_versions_deletes_all_rows()
    {
        using var context = CreateContext();
        var sut = new PreviewRepository(context);

        var versionId = Guid.NewGuid();
        var previews = new List<Preview>
        {
            PreviewRow(versionId, VersionInstance(versionId)),
            PreviewRow(versionId, VersionInstance(versionId))
        };

        var act = () => sut.RemoveRange(previews);

        act.Should().NotThrow();
        foreach (var preview in previews)
            context.Entry(preview).State.Should().Be(EntityState.Deleted);
    }
}