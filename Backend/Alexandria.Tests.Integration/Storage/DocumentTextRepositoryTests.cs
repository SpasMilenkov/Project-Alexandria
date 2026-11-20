using Alexandria.Common.Services;
using Alexandria.Data.Models;
using Alexandria.Data.Models.Enumerators;
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
        DateTime? createdAt = null)
    {
        await using var db = fixture.CreateDbContext();

        var content = sharedContentId is null
            ? new ContentObjectBuilder().WithIsPromoted(isPromoted).Build()
            : null;

        var versionBuilder = new FileVersionBuilder()
            .WithCreatedBy(_userId)
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
        var file = new FileBuilder()
            .WithOwner(_userId)
            .WithMimeType(mimeType)
            .WithDeletedAt(fileDeleted ? DateTime.UtcNow : null)
            .Build();
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