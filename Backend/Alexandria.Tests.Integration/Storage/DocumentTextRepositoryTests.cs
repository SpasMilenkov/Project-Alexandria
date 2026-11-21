using Alexandria.Common.Services;
using Alexandria.Data.Models;
using Alexandria.Data.Models.Enumerators;
using Alexandria.Dto.DocumentTextStats;
using Alexandria.Dto.Files;
using Alexandria.Repositories;
using Alexandria.Tests.Common.Builders;
using Alexandria.Tests.Common.Fixtures;
using AwesomeAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Alexandria.Tests.Integration.Storage;

/// <summary>
/// The search vector is computed in SQL and the candidate query leans on Postgres semantics,
/// so these run against a real database with every migration applied.
/// </summary>
[Collection("Database")]
public class DocumentTextRepositoryTests(DatabaseFixture fixture) : IAsyncLifetime
{
    private const string Pdf = "application/pdf";
    private const string Image = "image/png";

    private static readonly string[] Supported = [Pdf];

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
    public async Task Candidates_includePromotedUnencryptedLiveContent()
    {
        var seeded = await SeedVersionAsync();

        var candidates = await GetCandidatesAsync();

        candidates.Should().ContainSingle()
            .Which.Should().BeEquivalentTo(new
            {
                seeded.ContentObjectId,
                VersionId = seeded.Id,
                MimeType = Pdf
            });
    }

    [Fact]
    public async Task Candidates_skipContentNothingCanRead()
    {
        await SeedVersionAsync(mimeType: Image);
        await SeedVersionAsync(isPromoted: false);
        await SeedVersionAsync(isEncrypted: true);
        await SeedVersionAsync(fileDeleted: true);
        await SeedVersionAsync(versionDeleted: true);
        var readable = await SeedVersionAsync();

        var candidates = await GetCandidatesAsync();

        candidates.Select(c => c.ContentObjectId).Should().Equal(readable.ContentObjectId);
    }

    [Fact]
    public async Task Candidates_returnSharedContentOnce_withItsNewestVersion()
    {
        var original = await SeedVersionAsync(createdAt: DateTime.UtcNow.AddHours(-2));
        var copy = await SeedVersionAsync(sharedContentId: original.ContentObjectId, createdAt: DateTime.UtcNow);

        var candidates = await GetCandidatesAsync();

        candidates.Should().ContainSingle()
            .Which.VersionId.Should().Be(copy.Id);
    }

    [Fact]
    public async Task Candidates_orderNewestUploadFirst_andHonourTheLimit()
    {
        var oldest = await SeedVersionAsync(createdAt: DateTime.UtcNow.AddHours(-3));
        var middle = await SeedVersionAsync(createdAt: DateTime.UtcNow.AddHours(-2));
        var newest = await SeedVersionAsync(createdAt: DateTime.UtcNow.AddHours(-1));

        var candidates = await GetCandidatesAsync(limit: 2);

        candidates.Select(c => c.VersionId).Should().Equal(newest.Id, middle.Id);
        candidates.Should().NotContain(c => c.VersionId == oldest.Id);
    }

    [Fact]
    public async Task Candidates_skipFinalRowsAtCurrentVersion_butReturnOlderVersions()
    {
        var current = await SeedVersionAsync();
        var outdated = await SeedVersionAsync();

        await UpsertAsync(current.ContentObjectId, "current text");
        await UpsertAsync(outdated.ContentObjectId, "outdated text", extractorVersion: 0);

        var candidates = await GetCandidatesAsync();

        candidates.Select(c => c.ContentObjectId).Should().Equal(outdated.ContentObjectId);
    }

    [Fact]
    public async Task Candidates_retryFailedRowsAfterTheWindow_untilMaxAttempts()
    {
        var retryable = await SeedVersionAsync();
        var tooRecent = await SeedVersionAsync();
        var exhausted = await SeedVersionAsync();

        await RecordFailureAsync(retryable.ContentObjectId);
        await RecordFailureAsync(tooRecent.ContentObjectId);
        await RecordFailureAsync(exhausted.ContentObjectId);
        await RecordFailureAsync(exhausted.ContentObjectId);
        await RecordFailureAsync(exhausted.ContentObjectId);

        await SetLastAttemptAsync(retryable.ContentObjectId, DateTime.UtcNow.AddHours(-7));
        await SetLastAttemptAsync(exhausted.ContentObjectId, DateTime.UtcNow.AddHours(-7));

        var candidates = await GetCandidatesAsync(
            retryFailedBefore: DateTime.UtcNow.AddHours(-6), maxAttempts: 3);

        candidates.Select(c => c.ContentObjectId).Should().Equal(retryable.ContentObjectId);
    }

    [Fact]
    public async Task Upsert_storesTextThatSearchMatches()
    {
        var version = await SeedVersionAsync();

        var truncated = await UpsertAsync(version.ContentObjectId, "Hotel invoice from Paris\fPage two");

        truncated.Should().BeFalse();
        (await MatchesAsync(version.ContentObjectId, "invoice & paris")).Should().BeTrue();
        (await MatchesAsync(version.ContentObjectId, "berlin")).Should().BeFalse();

        var row = await GetRowAsync(version.ContentObjectId);
        row.Status.Should().Be(DocumentTextStatus.Extracted);
        row.Content.Should().Be("Hotel invoice from Paris\fPage two");
        row.IsTruncated.Should().BeFalse();
    }

    [Fact]
    public async Task Upsert_tooVariedText_indexesThePrefixAndMarksItTruncated()
    {
        var version = await SeedVersionAsync();

        // Unique tokens defeat the per-word position cap, so 1.3 M characters exceed the 1 MB
        // vector limit the way a sheet full of IDs would. The leading letter keeps the parser from
        // reading a token that starts with digits as a number and splitting it.
        var tokens = Enumerable.Range(0, 40_000).Select(_ => $"w{Guid.NewGuid():N}").ToList();
        var text = string.Join(' ', tokens);

        var truncated = await UpsertAsync(version.ContentObjectId, text);

        truncated.Should().BeTrue();

        var row = await GetRowAsync(version.ContentObjectId);
        row.IsTruncated.Should().BeTrue();
        row.Content.Should().HaveLength(text.Length);

        (await MatchesAsync(version.ContentObjectId, tokens[0])).Should().BeTrue();
        (await MatchesAsync(version.ContentObjectId, tokens[^1])).Should().BeFalse();
    }

    [Fact]
    public async Task Upsert_afterFailures_clearsTheErrorAndAttempts()
    {
        var version = await SeedVersionAsync();
        await RecordFailureAsync(version.ContentObjectId);
        await RecordFailureAsync(version.ContentObjectId);

        await UpsertAsync(version.ContentObjectId, "recovered text");

        var row = await GetRowAsync(version.ContentObjectId);
        row.Status.Should().Be(DocumentTextStatus.Extracted);
        row.ErrorDetail.Should().BeNull();
        row.AttemptCount.Should().Be(0);
    }

    [Fact]
    public async Task RecordFailure_countsConsecutiveFailures_andRestartsAtANewVersion()
    {
        var version = await SeedVersionAsync();

        await RecordFailureAsync(version.ContentObjectId, extractorVersion: 1);
        await RecordFailureAsync(version.ContentObjectId, extractorVersion: 1);
        (await GetRowAsync(version.ContentObjectId)).AttemptCount.Should().Be(2);

        await RecordFailureAsync(version.ContentObjectId, extractorVersion: 2, error: "pdftotext timed out");

        var row = await GetRowAsync(version.ContentObjectId);
        row.AttemptCount.Should().Be(1);
        row.ExtractorVersion.Should().Be(2);
        row.ErrorDetail.Should().Be("pdftotext timed out");
    }

    [Fact]
    public async Task RecordFailure_keepsEarlierTextSearchable()
    {
        var version = await SeedVersionAsync();
        await UpsertAsync(version.ContentObjectId, "quarterly budget report", extractorVersion: 1);

        await RecordFailureAsync(version.ContentObjectId, extractorVersion: 2);

        (await GetRowAsync(version.ContentObjectId)).Status.Should().Be(DocumentTextStatus.Failed);
        (await MatchesAsync(version.ContentObjectId, "budget")).Should().BeTrue();
    }

    [Fact]
    public async Task RecordFailure_cutsLongErrorsToTheColumnLength()
    {
        var version = await SeedVersionAsync();

        await RecordFailureAsync(version.ContentObjectId, error: new string('x', 5000));

        (await GetRowAsync(version.ContentObjectId)).ErrorDetail.Should()
            .HaveLength(ValidationConstants.StringLengths.LongString);
    }

    [Fact]
    public async Task BeginAttempt_thatNeverFinishes_usesUpTheAttempts()
    {
        // Each begin without an outcome is a worker that died while reading the document.
        var version = await SeedVersionAsync();
        var old = DateTime.UtcNow.AddDays(-1);

        for (var attempt = 0; attempt < 3; attempt++)
        {
            await BeginAttemptAsync(version.ContentObjectId);
            await SetLastAttemptAsync(version.ContentObjectId, old);
        }

        var row = await GetRowAsync(version.ContentObjectId);
        row.Status.Should().Be(DocumentTextStatus.Failed);
        row.AttemptCount.Should().Be(3);
        row.ErrorDetail.Should().StartWith("Reading stopped before it finished");
        (await GetCandidatesAsync(maxAttempts: 3)).Should().BeEmpty();
    }

    [Fact]
    public async Task FailAttempt_fillsInTheError_withoutCountingTwice()
    {
        var version = await SeedVersionAsync();

        await BeginAttemptAsync(version.ContentObjectId);
        await using (var db = fixture.CreateDbContext())
        {
            await new DocumentTextRepository(db).FailAttemptAsync(version.ContentObjectId, "damaged package", Ct);
        }

        var row = await GetRowAsync(version.ContentObjectId);
        row.AttemptCount.Should().Be(1);
        row.ErrorDetail.Should().Be("damaged package");
    }

    [Fact]
    public async Task AbandonAttempt_givesTheAttemptBack_andQueuesTheDocumentAtOnce()
    {
        var version = await SeedVersionAsync();
        await RecordFailureAsync(version.ContentObjectId, error: "pdftotext exited with 1");

        await BeginAttemptAsync(version.ContentObjectId);
        await using (var db = fixture.CreateDbContext())
        {
            await new DocumentTextRepository(db).AbandonAttemptAsync(version.ContentObjectId, Ct);
        }

        (await GetRowAsync(version.ContentObjectId)).Should().BeEquivalentTo(new
        {
            Status = DocumentTextStatus.Failed,
            AttemptCount = 1,
            LastAttemptAt = (DateTime?)null,
            ErrorDetail = (string?)null
        });
        (await GetCandidatesAsync()).Select(c => c.ContentObjectId).Should().Equal(version.ContentObjectId);
    }

    [Fact]
    public async Task BeginAttempt_thenUpsert_clearsTheAttempt()
    {
        var version = await SeedVersionAsync();

        await BeginAttemptAsync(version.ContentObjectId);
        await UpsertAsync(version.ContentObjectId, "read fine");

        var row = await GetRowAsync(version.ContentObjectId);
        row.Status.Should().Be(DocumentTextStatus.Extracted);
        row.AttemptCount.Should().Be(0);
        row.ErrorDetail.Should().BeNull();
    }

    [Fact]
    public async Task DeletingContent_removesItsText()
    {
        var version = await SeedVersionAsync();
        await UpsertAsync(version.ContentObjectId, "soon gone");

        await using (var db = fixture.CreateDbContext())
        {
            await db.FileVersions.Where(v => v.ContentObjectId == version.ContentObjectId).ExecuteDeleteAsync(Ct);
            await db.ContentObjects.Where(c => c.Id == version.ContentObjectId).ExecuteDeleteAsync(Ct);
        }

        await using var check = fixture.CreateDbContext();
        (await check.DocumentText.AnyAsync(Ct)).Should().BeFalse();
    }

    [Fact]
    public async Task StatusCounts_countLiveRowsPerOutcome_ofContentTheSweepCanRead()
    {
        var first = await SeedVersionAsync();
        var second = await SeedVersionAsync();
        var broken = await SeedVersionAsync();
        var trashed = await SeedVersionAsync(fileDeleted: true);
        var trashedBroken = await SeedVersionAsync(fileDeleted: true);

        await UpsertAsync(first.ContentObjectId, "one");
        await UpsertAsync(second.ContentObjectId, "two");
        await RecordFailureAsync(broken.ContentObjectId);
        await UpsertAsync(trashed.ContentObjectId, "in the trash");
        await RecordFailureAsync(trashedBroken.ContentObjectId);

        await using var db = fixture.CreateDbContext();
        var counts = await new DocumentTextRepository(db).GetStatusCountsAsync(Supported, Ct);

        counts.Should().Equal(
            new DocumentTextStatusCount(DocumentTextStatus.Extracted, 2),
            new DocumentTextStatusCount(DocumentTextStatus.Failed, 1));
    }

    [Fact]
    public async Task Waiting_countsReadableContentWithoutCurrentText()
    {
        var unread = await SeedVersionAsync();
        await SeedVersionAsync(sharedContentId: unread.ContentObjectId);
        var outdated = await SeedVersionAsync();
        var current = await SeedVersionAsync();
        var failed = await SeedVersionAsync();
        await SeedVersionAsync(mimeType: Image);

        await UpsertAsync(outdated.ContentObjectId, "old", extractorVersion: 0);
        await UpsertAsync(current.ContentObjectId, "new");
        await RecordFailureAsync(failed.ContentObjectId);

        await using var db = fixture.CreateDbContext();
        var waiting = await new DocumentTextRepository(db)
            .CountWaitingAsync(Supported, DocumentTextExtractor.CurrentVersion, Ct);

        // The unread content once despite its copy, plus the outdated text.
        waiting.Should().Be(2);
    }

    [Fact]
    public async Task RecentFailures_nameOnlyTheViewersFiles_andSayWhetherTheyRetry()
    {
        var otherUserId = Guid.NewGuid();
        await using (var seed = fixture.CreateDbContext())
        {
            seed.Users.Add(new UserBuilder().WithId(otherUserId).Build());
            await seed.SaveChangesAsync(Ct);
        }

        var own = await SeedVersionAsync(fileName: "my-scan.pdf");
        var others = await SeedVersionAsync(ownerId: otherUserId, fileName: "their-secret.pdf");

        await RecordFailureAsync(own.ContentObjectId, error: "pdftotext exited with 1");
        await RecordFailureAsync(others.ContentObjectId);
        await RecordFailureAsync(others.ContentObjectId);
        await RecordFailureAsync(others.ContentObjectId);

        await using var db = fixture.CreateDbContext();
        var failures = await new DocumentTextRepository(db)
            .GetRecentFailuresAsync(_userId, Supported, maxAttempts: 3, limit: 20, Ct);

        failures.Should().HaveCount(2);
        failures.Single(f => f.ContentObjectId == own.ContentObjectId).Should().BeEquivalentTo(new
        {
            FileName = "my-scan.pdf",
            MimeType = Pdf,
            ErrorDetail = "pdftotext exited with 1",
            AttemptCount = 1,
            WillRetry = true
        });
        failures.Single(f => f.ContentObjectId == others.ContentObjectId).Should().BeEquivalentTo(new
        {
            FileName = (string?)null,
            MimeType = (string?)null,
            ErrorDetail = (string?)null,
            AttemptCount = 3,
            WillRetry = false
        });
    }

    // Words that match none of the queries below, to push a match past the excerpt window.
    private static string Filler(int words) => string.Join(' ', Enumerable.Repeat("lorem ipsum dolor", words / 3));

    [Fact]
    public async Task FirstContentMatch_deepInALongText_matchesTheFullPassExcerpt()
    {
        var version = await SeedVersionAsync();
        await UpsertAsync(version.ContentObjectId,
            $"{Filler(3000)} the hotel invoice from Paris, total due 412 EUR. {Filler(300)} a second invoice {Filler(300)}");

        await using var db = fixture.CreateDbContext();
        var repository = new DocumentTextRepository(db);
        var first = await repository.GetFirstContentMatchAsync(version.ContentObjectId, "invoice", Ct);
        var full = await repository.GetContentMatchesAsync(version.ContentObjectId, "invoice", Ct);

        first.Status.Should().Be(DocumentTextStatus.Extracted);
        first.Fragments.Should().ContainSingle()
            .Which.Should().BeEquivalentTo(full.Fragments[0]);
        string.Concat(first.Fragments[0].Segments.Select(s => s.Text)).Should().Contain("hotel invoice from Paris");
    }

    [Fact]
    public async Task FirstContentMatch_wordOnlyInsideAPathAtFirst_fallsBackToTheFullPass()
    {
        // Postgres reads docs/article1.html as one path token, so "art" does not match there even
        // though the regex finds a word start after the slash.
        var version = await SeedVersionAsync();
        await UpsertAsync(version.ContentObjectId,
            $"Open docs/article1.html first. {Filler(600)} It keeps a list of artifacts it created.");

        await using var db = fixture.CreateDbContext();
        var match = await new DocumentTextRepository(db)
            .GetFirstContentMatchAsync(version.ContentObjectId, "art", Ct);

        match.Fragments.Should().ContainSingle()
            .Which.Segments.Where(s => s.IsMatch).Select(s => s.Text).Should().Equal("artifacts");
    }

    [Fact]
    public async Task FirstContentMatch_wordsFarApart_fallsBackToTheFullPass()
    {
        var version = await SeedVersionAsync();
        await UpsertAsync(version.ContentObjectId,
            $"The air was cold. {Filler(600)} The temperature kept falling.");

        await using var db = fixture.CreateDbContext();
        var match = await new DocumentTextRepository(db)
            .GetFirstContentMatchAsync(version.ContentObjectId, "air temp", Ct);

        match.Fragments.Should().ContainSingle()
            .Which.Segments.Should().Contain(s => s.IsMatch);
    }

    [Fact]
    public async Task FirstContentMatch_noMatch_keepsTheStatus_withoutReadingAnExcerpt()
    {
        var version = await SeedVersionAsync();
        await UpsertAsync(version.ContentObjectId, "Hotel invoice from Paris");

        await using var db = fixture.CreateDbContext();
        var match = await new DocumentTextRepository(db)
            .GetFirstContentMatchAsync(version.ContentObjectId, "berlin", Ct);

        match.Status.Should().Be(DocumentTextStatus.Extracted);
        match.Fragments.Should().BeEmpty();
    }

    [Fact]
    public async Task FirstContentMatch_caseAndScriptInsensitive_likeTheTextSearch()
    {
        var version = await SeedVersionAsync();
        await UpsertAsync(version.ContentObjectId,
            $"{Filler(600)} Изкуственият интелект и ГОЛЕМИ данни.");

        await using var db = fixture.CreateDbContext();
        var match = await new DocumentTextRepository(db)
            .GetFirstContentMatchAsync(version.ContentObjectId, "изкуствен голем", Ct);

        match.Fragments.Should().ContainSingle()
            .Which.Segments.Where(s => s.IsMatch).Select(s => s.Text).Should().Equal("Изкуственият", "ГОЛЕМИ");
    }

    [Fact]
    public async Task ResetFailed_givesExhaustedFailuresFreshAttempts()
    {
        var exhausted = await SeedVersionAsync();
        var extracted = await SeedVersionAsync();
        var trashed = await SeedVersionAsync(fileDeleted: true);

        await RecordFailureAsync(exhausted.ContentObjectId);
        await RecordFailureAsync(exhausted.ContentObjectId);
        await RecordFailureAsync(exhausted.ContentObjectId);
        await UpsertAsync(extracted.ContentObjectId, "fine");
        await RecordFailureAsync(trashed.ContentObjectId);

        (await GetCandidatesAsync(maxAttempts: 3)).Should().BeEmpty();

        // The trashed file's failure is left alone: the sweep would never pick it up.
        await using (var db = fixture.CreateDbContext())
        {
            (await new DocumentTextRepository(db).ResetFailedAsync(Supported, Ct)).Should().Be(1);
        }

        (await GetCandidatesAsync(maxAttempts: 3)).Select(c => c.ContentObjectId)
            .Should().Equal(exhausted.ContentObjectId);
        (await GetRowAsync(exhausted.ContentObjectId)).Should().BeEquivalentTo(new
        {
            Status = DocumentTextStatus.Failed,
            AttemptCount = 0,
            LastAttemptAt = (DateTime?)null
        });
    }

    private async Task<IReadOnlyList<DocumentTextCandidate>> GetCandidatesAsync(
        DateTime? retryFailedBefore = null, int maxAttempts = 3, int limit = 50)
    {
        await using var db = fixture.CreateDbContext();
        var repository = new DocumentTextRepository(db);

        return await repository.GetExtractionCandidatesAsync(
            Supported,
            DocumentTextExtractor.CurrentVersion,
            retryFailedBefore ?? DateTime.UtcNow.AddHours(-6),
            maxAttempts,
            limit,
            Ct);
    }

    private async Task<bool> UpsertAsync(Guid contentObjectId, string content,
        int extractorVersion = DocumentTextExtractor.CurrentVersion)
    {
        await using var db = fixture.CreateDbContext();
        var repository = new DocumentTextRepository(db);

        return await repository.UpsertAsync(new DocumentText
        {
            Id = Guid.NewGuid(),
            ContentObjectId = contentObjectId,
            Status = DocumentTextStatus.Extracted,
            Content = content,
            CharacterCount = content.Length,
            ExtractorVersion = extractorVersion
        }, Ct);
    }

    private async Task RecordFailureAsync(Guid contentObjectId,
        int extractorVersion = DocumentTextExtractor.CurrentVersion, string error = "boom")
    {
        await using var db = fixture.CreateDbContext();
        var repository = new DocumentTextRepository(db);

        await repository.RecordFailureAsync(contentObjectId, extractorVersion, error, Ct);
    }

    private async Task BeginAttemptAsync(Guid contentObjectId)
    {
        await using var db = fixture.CreateDbContext();

        await new DocumentTextRepository(db).BeginAttemptAsync(contentObjectId, DocumentTextExtractor.CurrentVersion, Ct);
    }

    private async Task SetLastAttemptAsync(Guid contentObjectId, DateTime lastAttemptAt)
    {
        await using var db = fixture.CreateDbContext();

        await db.DocumentText
            .Where(t => t.ContentObjectId == contentObjectId)
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.LastAttemptAt, lastAttemptAt), Ct);
    }

    private async Task<DocumentText> GetRowAsync(Guid contentObjectId)
    {
        await using var db = fixture.CreateDbContext();

        return await db.DocumentText.AsNoTracking().SingleAsync(t => t.ContentObjectId == contentObjectId, Ct);
    }

    private async Task<bool> MatchesAsync(Guid contentObjectId, string tsQuery)
    {
        await using var db = fixture.CreateDbContext();

        return await db.DocumentText.AnyAsync(t =>
            t.ContentObjectId == contentObjectId
            && t.SearchVector.Matches(EF.Functions.ToTsQuery("simple", tsQuery)), Ct);
    }

    private async Task<FileVersion> SeedVersionAsync(
        string mimeType = Pdf,
        bool isPromoted = true,
        bool isEncrypted = false,
        bool fileDeleted = false,
        bool versionDeleted = false,
        Guid? sharedContentId = null,
        DateTime? createdAt = null,
        Guid? ownerId = null,
        string? fileName = null)
    {
        await using var db = fixture.CreateDbContext();
        var owner = ownerId ?? _userId;

        var content = sharedContentId is null
            ? new ContentObjectBuilder().WithIsPromoted(isPromoted).Build()
            : null;

        var versionBuilder = new FileVersionBuilder()
            .WithCreatedBy(owner)
            .WithMimeType(mimeType)
            .WithCreatedAt(createdAt ?? DateTime.UtcNow)
            .WithDeletedAt(versionDeleted ? DateTime.UtcNow : null);

        if (content is not null)
            versionBuilder = versionBuilder.WithContentObject(content);
        else
            versionBuilder = versionBuilder.WithContentObjectId(sharedContentId!.Value);

        if (isEncrypted)
        {
            versionBuilder = versionBuilder
                .WithIsEncrypted(true)
                .WithEncryptionIv(new byte[12])
                .WithEncryptionSalt(new byte[16])
                .WithIntegrityTag(new byte[16]);
        }

        var version = versionBuilder.Build();
        var fileBuilder = new FileBuilder()
            .WithOwner(owner)
            .WithMimeType(mimeType)
            .WithDeletedAt(fileDeleted ? DateTime.UtcNow : null);
        if (fileName is not null)
            fileBuilder = fileBuilder.WithName(fileName);

        var file = fileBuilder.Build();
        version.File = file;

        if (content is not null)
            db.Add(content);

        db.AddRange(version, file);
        await db.SaveChangesAsync(Ct);

        file.CurrentVersionId = version.Id;
        await db.SaveChangesAsync(Ct);

        return version;
    }
}