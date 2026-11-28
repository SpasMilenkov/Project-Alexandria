using Alexandria.Data.Models;
using Alexandria.Data.Models.Enumerators;
using Alexandria.Dto.Files.Streaming;
using Alexandria.Repositories;
using Alexandria.Tests.Common.Builders;
using Alexandria.Tests.Common.Fixtures;
using AwesomeAssertions;
using Xunit;
using Directory = Alexandria.Data.Models.Directory;
using File = Alexandria.Data.Models.File;

namespace Alexandria.Tests.Integration.Storage;

/// <summary>
/// The latest transcode per version is picked inside the SQL query, and a version without one
/// has to come back as null rather than an empty record, which only a real database shows.
/// </summary>
[Collection("Database")]
public class TranscodeBackfillCandidateTests(DatabaseFixture fixture) : IAsyncLifetime
{
    private readonly Guid _userId = Guid.NewGuid();
    private readonly Guid _secondUserId = Guid.NewGuid();
    private readonly Guid _thirdUserId = Guid.NewGuid();
    private readonly DateTime _cutoff = DateTime.UtcNow.AddMinutes(1);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync()
    {
        await using var db = fixture.CreateDbContext();
        db.Users.Add(new UserBuilder().WithId(_userId).Build());
        db.Users.Add(new UserBuilder().WithId(_secondUserId).Build());
        db.Users.Add(new UserBuilder().WithId(_thirdUserId).Build());
        await db.SaveChangesAsync(Ct);
    }

    public async ValueTask DisposeAsync()
    {
        await fixture.ResetAsync();
    }

    [Fact]
    public async Task Candidates_neverTranscodedVersion_comesBackWithoutATranscode()
    {
        var music = await SeedDirectoryAsync("Music");
        var (file, version) = await SeedFileAsync(music.Id);

        var candidates = await GetCandidatesAsync([music.Id]);

        candidates.Should().ContainSingle().Which.Should().BeEquivalentTo(new TranscodeBackfillCandidate
        {
            FileId = file.Id,
            VersionId = version.Id,
            OwnerId = _userId,
            MimeType = "audio/mpeg",
            Transcode = null,
        });
    }

    [Fact]
    public async Task Candidates_versionWithSeveralTranscodes_carriesTheNewest()
    {
        var music = await SeedDirectoryAsync("Music");
        var (_, version) = await SeedFileAsync(music.Id);

        await SeedTranscodeAsync(version.Id, _userId, DateTime.UtcNow.AddHours(-2), JobStatus.Ready,
            [AudioRung.Kbps128]);
        var newest = await SeedTranscodeAsync(version.Id, _secondUserId, DateTime.UtcNow.AddHours(-1),
            JobStatus.Failed, [AudioRung.Kbps192, AudioRung.Kbps320]);

        var candidates = await GetCandidatesAsync([music.Id]);

        candidates.Should().ContainSingle().Which.Transcode.Should().BeEquivalentTo(new ExistingTranscode
        {
            TranspilationId = newest.Id,
            UserId = _secondUserId,
            IsVideo = false,
            Status = JobStatus.Failed,
            AudioRungs = [AudioRung.Kbps192, AudioRung.Kbps320],
            VideoRungs = [],
        });
    }

    [Fact]
    public async Task Candidates_newerDeletedTranscodeOrJob_fallsBackToTheLiveOne()
    {
        var music = await SeedDirectoryAsync("Music");
        var (_, version) = await SeedFileAsync(music.Id);

        var live = await SeedTranscodeAsync(version.Id, _userId, DateTime.UtcNow.AddHours(-3), JobStatus.Ready,
            [AudioRung.Kbps128]);
        await SeedTranscodeAsync(version.Id, _secondUserId, DateTime.UtcNow.AddHours(-2), JobStatus.Ready,
            [AudioRung.Kbps192], deleted: true);
        await SeedTranscodeAsync(version.Id, _thirdUserId, DateTime.UtcNow.AddHours(-1), JobStatus.Ready,
            [AudioRung.Kbps256], jobDeleted: true);

        var candidates = await GetCandidatesAsync([music.Id]);

        candidates.Should().ContainSingle().Which.Transcode!.TranspilationId.Should().Be(live.Id);
    }

    [Fact]
    public async Task Candidates_onlyLivePredatingFilesWithAVersionInTheGivenFolders()
    {
        var music = await SeedDirectoryAsync("Music");
        var other = await SeedDirectoryAsync("Other");

        var (inScope, _) = await SeedFileAsync(music.Id);
        await SeedFileAsync(music.Id, deleted: true);
        await SeedFileAsync(music.Id, createdAt: _cutoff.AddHours(1));
        await SeedFileAsync(other.Id);
        await SeedFileWithoutVersionAsync(music.Id);
        await SeedFileAsync(music.Id, encrypted: true);

        var candidates = await GetCandidatesAsync([music.Id]);

        candidates.Select(c => c.FileId).Should().Equal(inScope.Id);
    }

    [Fact]
    public async Task Candidates_onlyAudioAndVideoTypes_inAnyCase()
    {
        var mixed = await SeedDirectoryAsync("Mixed");

        var (audio, _) = await SeedFileAsync(mixed.Id, mimeType: "audio/flac");
        var (video, _) = await SeedFileAsync(mixed.Id, mimeType: "Video/MP4");
        await SeedFileAsync(mixed.Id, mimeType: "image/png");
        await SeedFileAsync(mixed.Id, mimeType: "application/pdf");

        var candidates = await GetCandidatesAsync([mixed.Id]);

        candidates.Select(c => c.FileId).Should().BeEquivalentTo(new[] { audio.Id, video.Id });
    }

    private async Task<IReadOnlyList<TranscodeBackfillCandidate>> GetCandidatesAsync(Guid[] directoryIds)
    {
        await using var db = fixture.CreateDbContext();

        return await new TranspilationJobRepository(db)
            .GetTranscodeBackfillCandidatesAsync(directoryIds, _cutoff, Ct);
    }

    private async Task<Directory> SeedDirectoryAsync(string name)
    {
        await using var db = fixture.CreateDbContext();
        var directory = new DirectoryBuilder().WithOwner(_userId).WithName(name).Build();

        db.Directories.Add(directory);
        await db.SaveChangesAsync(Ct);

        return directory;
    }

    private async Task<(File File, FileVersion Version)> SeedFileAsync(
        Guid directoryId,
        string mimeType = "audio/mpeg",
        DateTime? createdAt = null,
        bool deleted = false,
        bool encrypted = false)
    {
        DateTime? deletedAt = null;
        if (deleted) deletedAt = DateTime.UtcNow;

        await using var db = fixture.CreateDbContext();

        var content = new ContentObjectBuilder().Build();
        var versionBuilder = new FileVersionBuilder()
            .WithContentObject(content)
            .WithCreatedBy(_userId)
            .WithMimeType(mimeType);

        if (encrypted)
        {
            versionBuilder
                .WithIsEncrypted(true)
                .WithEncryptionIv(new byte[12])
                .WithEncryptionSalt(new byte[16])
                .WithIntegrityTag(new byte[16]);
        }

        var version = versionBuilder.Build();
        var file = new FileBuilder()
            .WithOwner(_userId)
            .WithDirectory(directoryId)
            .WithMimeType(mimeType)
            .WithCreatedAt(createdAt ?? DateTime.UtcNow)
            .WithDeletedAt(deletedAt)
            .Build();

        version.File = file;
        db.AddRange(content, version, file);
        await db.SaveChangesAsync(Ct);

        // Set after the insert because File and FileVersion reference each other
        file.CurrentVersionId = version.Id;
        await db.SaveChangesAsync(Ct);

        return (file, version);
    }

    private async Task SeedFileWithoutVersionAsync(Guid directoryId)
    {
        await using var db = fixture.CreateDbContext();

        db.Files.Add(new FileBuilder()
            .WithOwner(_userId)
            .WithDirectory(directoryId)
            .WithMimeType("audio/mpeg")
            .Build());
        await db.SaveChangesAsync(Ct);
    }

    private async Task<TranspilationJob> SeedTranscodeAsync(
        Guid versionId,
        Guid userId,
        DateTime createdAt,
        JobStatus status,
        AudioRung[] audioRungs,
        bool deleted = false,
        bool jobDeleted = false)
    {
        DateTime? deletedAt = null;
        if (deleted) deletedAt = DateTime.UtcNow;

        DateTime? jobDeletedAt = null;
        if (jobDeleted) jobDeletedAt = DateTime.UtcNow;

        await using var db = fixture.CreateDbContext();

        var job = new JobBuilder()
            .WithStatus(status)
            .WithType(JobType.Transpilation)
            .WithUser(userId)
            .WithDeletedAt(jobDeletedAt)
            .Build();

        var transcode = new TranspilationJob
        {
            Id = Guid.NewGuid(),
            VersionId = versionId,
            JobId = job.Id,
            IsVideo = false,
            UserId = userId,
            AudioRungs = audioRungs,
            CreatedAt = createdAt,
            DeletedAt = deletedAt,
        };

        db.AddRange(job, transcode);
        await db.SaveChangesAsync(Ct);

        return transcode;
    }
}
