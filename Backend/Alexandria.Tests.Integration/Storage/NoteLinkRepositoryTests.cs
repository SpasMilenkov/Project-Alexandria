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
/// "Is this a note" is a recursive folder walk in SQL, so the candidate and backlink queries run
/// against a real database with every migration applied.
/// </summary>
[Collection("Database")]
public class NoteLinkRepositoryTests(DatabaseFixture fixture) : IAsyncLifetime
{
    private const string Markdown = "text/markdown";
    private const int Limit = 50;
    private const long MaxBytes = 1_000_000;

    private readonly Guid _userId = Guid.NewGuid();
    private readonly Guid _otherUserId = Guid.NewGuid();
    private readonly Guid _targetId = Guid.NewGuid();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync()
    {
        await using var db = fixture.CreateDbContext();
        db.Users.Add(new UserBuilder().WithId(_userId).Build());
        db.Users.Add(new UserBuilder().WithId(_otherUserId).Build());
        await db.SaveChangesAsync(Ct);
    }

    public async ValueTask DisposeAsync()
    {
        await fixture.ResetAsync();
    }

    [Fact]
    public async Task Candidates_includeUnreadNotesAnywhereBelowNotes()
    {
        var notes = await SeedFolderAsync(_userId, parentId: null, isNotes: true);
        var project = await SeedFolderAsync(_userId, notes.Id);
        var top = await SeedFileAsync(_userId, notes.Id);
        var nested = await SeedFileAsync(_userId, project.Id);

        var candidates = await GetCandidatesAsync();

        candidates.Select(c => c.VersionId).Should().BeEquivalentTo([top.Id, nested.Id]);
        candidates.Should().OnlyContain(c => c.OwnerId == _userId);
    }

    [Fact]
    public async Task Candidates_skipWhatIsNotALiveReadableNote()
    {
        var notes = await SeedFolderAsync(_userId, parentId: null, isNotes: true);
        var trashedFolder = await SeedFolderAsync(_userId, notes.Id, deleted: true);
        var elsewhere = await SeedFolderAsync(_userId, parentId: null);

        await SeedFileAsync(_userId, elsewhere.Id);
        await SeedFileAsync(_userId, null);
        await SeedFileAsync(_userId, trashedFolder.Id);
        await SeedFileAsync(_userId, notes.Id, mimeType: "image/png");
        await SeedFileAsync(_userId, notes.Id, fileDeleted: true);
        await SeedFileAsync(_userId, notes.Id, isEncrypted: true);
        await SeedFileAsync(_userId, notes.Id, size: MaxBytes + 1);
        var readable = await SeedFileAsync(_userId, notes.Id);

        var candidates = await GetCandidatesAsync();

        candidates.Select(c => c.VersionId).Should().Equal(readable.Id);
    }

    [Fact]
    public async Task Candidates_skipContentAlreadyReadByTheCurrentExtractor()
    {
        var notes = await SeedFolderAsync(_userId, parentId: null, isNotes: true);
        var read = await SeedFileAsync(_userId, notes.Id);
        var readByOlder = await SeedFileAsync(_userId, notes.Id);

        await StoreScanAsync(read.ContentObjectId, scannerVersion: 2);
        await StoreScanAsync(readByOlder.ContentObjectId, scannerVersion: 1);

        var candidates = await GetCandidatesAsync(scannerVersion: 2);

        candidates.Select(c => c.VersionId).Should().Equal(readByOlder.Id);
    }

    [Fact]
    public async Task Replace_overAnOlderScan_swapsTheLinks()
    {
        var notes = await SeedFolderAsync(_userId, parentId: null, isNotes: true);
        var note = await SeedFileAsync(_userId, notes.Id);
        var oldTarget = Guid.NewGuid();

        await StoreScanAsync(note.ContentObjectId, scannerVersion: 1, targetId: oldTarget);
        var stored = await StoreScanAsync(note.ContentObjectId, scannerVersion: 2, targetId: _targetId);

        stored.Should().BeTrue();

        await using var db = fixture.CreateDbContext();
        db.NoteLinks.Where(l => l.ContentObjectId == note.ContentObjectId).Select(l => l.TargetId)
            .Should().Equal(_targetId);
    }

    [Fact]
    public async Task Replace_whenTheInsertFails_keepsTheOldScanAndLeavesTheContextUsable()
    {
        var notes = await SeedFolderAsync(_userId, parentId: null, isNotes: true);
        var note = await SeedFileAsync(_userId, notes.Id);
        var oldTarget = Guid.NewGuid();

        await StoreScanAsync(note.ContentObjectId, scannerVersion: 1, targetId: oldTarget);

        await using var db = fixture.CreateDbContext();
        var repository = new NoteLinkRepository(db);

        // Postgres refuses U+0000 in text, so the insert fails after the old scan's delete has run.
        var act = () => repository.ReplaceAsync(NewScan(note.ContentObjectId, 2, _targetId, "bad\0line"), Ct);

        await act.Should().ThrowAsync<DbUpdateException>();
        db.ChangeTracker.Entries().Should().BeEmpty();

        var kept = await StoredScanAsync(note.ContentObjectId);
        kept.ScannerVersion.Should().Be(1);
        kept.Targets.Should().Equal(oldTarget);

        // The same context still saves, as the rest of a checkpoint request goes on to do.
        (await repository.ReplaceAsync(NewScan(note.ContentObjectId, 2, _targetId), Ct)).Should().BeTrue();

        var replaced = await StoredScanAsync(note.ContentObjectId);
        replaced.ScannerVersion.Should().Be(2);
        replaced.Targets.Should().Equal(_targetId);
    }

    [Fact]
    public async Task Replace_failingInsideACallersTransaction_undoesOnlyItsOwnWork()
    {
        var notes = await SeedFolderAsync(_userId, parentId: null, isNotes: true);
        var note = await SeedFileAsync(_userId, notes.Id);

        await StoreScanAsync(note.ContentObjectId, scannerVersion: 1, targetId: Guid.NewGuid());

        await using var db = fixture.CreateDbContext();
        var repository = new NoteLinkRepository(db);
        await using var transaction = await db.Database.BeginTransactionAsync(Ct);

        var act = () => repository.ReplaceAsync(NewScan(note.ContentObjectId, 2, _targetId, "bad\0line"), Ct);

        await act.Should().ThrowAsync<DbUpdateException>();

        // A failed statement aborts a Postgres transaction outright; the savepoint is what lets the
        // caller carry on and commit.
        (await repository.ReplaceAsync(NewScan(note.ContentObjectId, 2, _targetId), Ct)).Should().BeTrue();
        await transaction.CommitAsync(Ct);

        var stored = await StoredScanAsync(note.ContentObjectId);
        stored.ScannerVersion.Should().Be(2);
        stored.Targets.Should().Equal(_targetId);
    }

    [Fact]
    public async Task Backlinks_listTheOwnersNotesLinkingToTheTarget_newestFirst()
    {
        var notes = await SeedFolderAsync(_userId, parentId: null, isNotes: true);
        var project = await SeedFolderAsync(_userId, notes.Id);
        var older = await SeedFileAsync(_userId, notes.Id, name: "Older.md", updatedAt: DateTime.UtcNow.AddHours(-2));
        var newer = await SeedFileAsync(_userId, project.Id, name: "Newer.md", updatedAt: DateTime.UtcNow.AddHours(-1));
        var unrelated = await SeedFileAsync(_userId, notes.Id);

        await StoreScanAsync(older.ContentObjectId, targetId: _targetId, snippet: "older line");
        await StoreScanAsync(newer.ContentObjectId, targetId: _targetId, snippet: "newer line");
        await StoreScanAsync(unrelated.ContentObjectId, targetId: Guid.NewGuid());

        var backlinks = await GetBacklinksAsync();

        backlinks.Select(b => (b.Name, b.Snippet)).Should().Equal(("Newer.md", "newer line"), ("Older.md", "older line"));
        backlinks[0].DirectoryId.Should().Be(project.Id);
    }

    [Fact]
    public async Task Backlinks_leaveOutNotesThatNoLongerCount()
    {
        var notes = await SeedFolderAsync(_userId, parentId: null, isNotes: true);
        var elsewhere = await SeedFolderAsync(_userId, parentId: null);
        var othersNotes = await SeedFolderAsync(_otherUserId, parentId: null, isNotes: true);

        var movedOut = await SeedFileAsync(_userId, elsewhere.Id);
        var trashed = await SeedFileAsync(_userId, notes.Id, fileDeleted: true);
        var someoneElses = await SeedFileAsync(_otherUserId, othersNotes.Id);
        var live = await SeedFileAsync(_userId, notes.Id);

        foreach (var version in new[] { movedOut, trashed, someoneElses, live })
            await StoreScanAsync(version.ContentObjectId, targetId: _targetId);

        var backlinks = await GetBacklinksAsync();

        backlinks.Select(b => b.FileId).Should().Equal(live.FileId);
    }

    [Fact]
    public async Task Backlinks_readOnlyTheCurrentVersion()
    {
        var notes = await SeedFolderAsync(_userId, parentId: null, isNotes: true);
        var note = await SeedFileAsync(_userId, notes.Id);

        // An older version linked to the target; the current one doesn't.
        await StoreScanAsync(note.ContentObjectId, targetId: Guid.NewGuid());
        await using (var db = fixture.CreateDbContext())
        {
            var oldContent = new ContentObjectBuilder().Build();
            var oldVersion = new FileVersionBuilder().WithCreatedBy(_userId).WithMimeType(Markdown)
                .WithContentObject(oldContent).WithFileId(note.FileId).WithVersionNumber(0).Build();

            db.AddRange(oldContent, oldVersion);
            await db.SaveChangesAsync(Ct);

            await StoreScanAsync(oldContent.Id, targetId: _targetId);
        }

        var backlinks = await GetBacklinksAsync();

        backlinks.Should().BeEmpty();
    }

    [Fact]
    public async Task Backlinks_aNoteLinkingToItself_isNotListed()
    {
        var notes = await SeedFolderAsync(_userId, parentId: null, isNotes: true);
        var note = await SeedFileAsync(_userId, notes.Id);

        await StoreScanAsync(note.ContentObjectId, targetId: note.FileId);

        await using var db = fixture.CreateDbContext();
        var backlinks = await new NoteLinkRepository(db)
            .GetBacklinksAsync(LinkTargetKind.File, note.FileId, _userId, Limit, Ct);

        backlinks.Should().BeEmpty();
    }

    [Fact]
    public async Task CountBacklinks_countsTheOwnersNotesPerTarget_andLeavesOutTheUnmentioned()
    {
        var notes = await SeedFolderAsync(_userId, parentId: null, isNotes: true);
        var first = await SeedFileAsync(_userId, notes.Id);
        var second = await SeedFileAsync(_userId, notes.Id);
        var trashed = await SeedFileAsync(_userId, notes.Id, fileDeleted: true);
        var unmentioned = Guid.NewGuid();

        foreach (var version in new[] { first, second, trashed })
            await StoreScanAsync(version.ContentObjectId, targetId: _targetId);

        await using var db = fixture.CreateDbContext();
        var counts = await new NoteLinkRepository(db)
            .CountBacklinksAsync(LinkTargetKind.File, [_targetId, unmentioned], _userId, Ct);

        counts.Should().ContainSingle()
            .Which.Should().BeEquivalentTo(new { TargetId = _targetId, Count = 2 });
    }

    private async Task<IReadOnlyList<Alexandria.Dto.Links.NoteLinkScanCandidate>> GetCandidatesAsync(
        int scannerVersion = 1)
    {
        await using var db = fixture.CreateDbContext();

        return await new NoteLinkRepository(db).GetScanCandidatesAsync(scannerVersion, MaxBytes, Limit, Ct);
    }

    private async Task<IReadOnlyList<Alexandria.Dto.Links.NoteBacklinkDto>> GetBacklinksAsync()
    {
        await using var db = fixture.CreateDbContext();

        return await new NoteLinkRepository(db).GetBacklinksAsync(LinkTargetKind.File, _targetId, _userId, Limit, Ct);
    }

    private async Task<bool> StoreScanAsync(Guid contentObjectId, int scannerVersion = 1, Guid? targetId = null,
        string snippet = "a line")
    {
        await using var db = fixture.CreateDbContext();

        return await new NoteLinkRepository(db).ReplaceAsync(
            NewScan(contentObjectId, scannerVersion, targetId, snippet), Ct);
    }

    private static NoteLinkScan NewScan(Guid contentObjectId, int scannerVersion, Guid? targetId,
        string snippet = "a line")
    {
        var scan = new NoteLinkScan
        {
            ContentObjectId = contentObjectId,
            ScannerVersion = scannerVersion,
        };

        if (targetId is { } target)
        {
            scan.Links.Add(new NoteLink
            {
                ContentObjectId = contentObjectId,
                TargetKind = LinkTargetKind.File,
                TargetId = target,
                Snippet = snippet,
            });
        }

        return scan;
    }

    private async Task<(int? ScannerVersion, List<Guid> Targets)> StoredScanAsync(Guid contentObjectId)
    {
        await using var db = fixture.CreateDbContext();

        var version = await db.NoteLinkScans
            .Where(s => s.ContentObjectId == contentObjectId)
            .Select(s => (int?)s.ScannerVersion)
            .FirstOrDefaultAsync(Ct);
        var targets = await db.NoteLinks
            .Where(l => l.ContentObjectId == contentObjectId)
            .Select(l => l.TargetId)
            .ToListAsync(Ct);

        return (version, targets);
    }

    private async Task<Directory> SeedFolderAsync(Guid ownerId, Guid? parentId, bool isNotes = false,
        bool deleted = false)
    {
        await using var db = fixture.CreateDbContext();

        var folder = new DirectoryBuilder()
            .WithOwner(ownerId)
            .WithParent(parentId)
            .WithName($"Folder {Guid.NewGuid():N}")
            .WithDeletedAt(deleted ? DateTime.UtcNow : null)
            .Build();

        if (isNotes) folder.SystemKind = DirectorySystemKind.Notes;

        db.Add(folder);
        await db.SaveChangesAsync(Ct);

        return folder;
    }

    private async Task<FileVersion> SeedFileAsync(
        Guid ownerId,
        Guid? directoryId,
        string mimeType = Markdown,
        bool fileDeleted = false,
        bool isEncrypted = false,
        long size = 100,
        string? name = null,
        DateTime? updatedAt = null)
    {
        await using var db = fixture.CreateDbContext();

        var content = new ContentObjectBuilder().Build();
        var versionBuilder = new FileVersionBuilder()
            .WithCreatedBy(ownerId)
            .WithMimeType(mimeType)
            .WithSize(size)
            .WithContentObject(content);

        if (isEncrypted)
        {
            versionBuilder = versionBuilder
                .WithIsEncrypted(true)
                .WithEncryptionIv(new byte[12])
                .WithEncryptionSalt(new byte[16])
                .WithIntegrityTag(new byte[16]);
        }

        var version = versionBuilder.Build();
        var file = new FileBuilder()
            .WithOwner(ownerId)
            .WithDirectory(directoryId)
            .WithMimeType(mimeType)
            .WithName(name ?? $"{Guid.NewGuid():N}.md")
            .WithDeletedAt(fileDeleted ? DateTime.UtcNow : null)
            .WithUpdatedAt(updatedAt ?? DateTime.UtcNow)
            .Build();

        version.File = file;

        db.AddRange(content, version, file);
        await db.SaveChangesAsync(Ct);

        file.CurrentVersionId = version.Id;
        await db.SaveChangesAsync(Ct);

        return version;
    }
}
