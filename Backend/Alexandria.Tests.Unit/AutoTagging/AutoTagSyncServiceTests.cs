using System.Linq.Expressions;
using Alexandria.Common;
using Alexandria.Common.Repositories;
using Alexandria.Common.Services;
using Alexandria.Common.Settings.Values;
using Alexandria.Data.Models;
using Alexandria.Data.Models.Enumerators;
using Alexandria.Dto.Autotag;
using Alexandria.Services.Storage.AutoTagging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using File = Alexandria.Data.Models.File;

namespace Alexandria.Tests.Unit.AutoTagging;

public class AutoTagSyncServiceTests
{
    private static readonly Guid FileId = Guid.Parse("aaaa0000-0000-0000-0000-000000000001");
    private static readonly Guid OwnerId = Guid.Parse("aaaa0000-0000-0000-0000-000000000002");
    private static readonly Guid GenreTagId = Guid.Parse("aaaa0000-0000-0000-0000-000000000003");
    private static readonly Guid MoodTagId = Guid.Parse("aaaa0000-0000-0000-0000-000000000004");

    [Fact]
    public async Task SyncFileAsync_prefers_highest_priority_backbone()
    {
        var (service, uow, derivation, settings, fileTags) = CreateService();
        uow.Files.GetByIdAsync(FileId, Arg.Any<CancellationToken>())
            .Returns(File());
        uow.FileEnrichments.FindAsync(Arg.Any<Expression<Func<FileEnrichment, bool>>>(), Arg.Any<CancellationToken>())
            .Returns(
            [
                GenreRow("essentia-genre-maest", """{"backbone":"maest","predictions":[]}"""),
                GenreRow("essentia-genre-effnet", """{"backbone":"effnet","predictions":[]}"""),
                MoodRow(),
            ]);
        derivation.DeriveAsync(Arg.Any<IReadOnlyCollection<FileEnrichment>>(), Arg.Any<CancellationToken>())
            .Returns([Candidate(GenreTagId, 0.9, TagFacet.Genre)]);
        settings.GetBehaviorAsync(OwnerId, Arg.Any<CancellationToken>())
            .Returns(new BehaviorSettingsValue());

        await service.SyncFileAsync(FileId, CancellationToken.None);

        await derivation.Received(1).DeriveAsync(
            Arg.Is<IReadOnlyCollection<FileEnrichment>>(rows =>
                rows.Count == 2
                && rows.Any(r => r.Analyzer == "essentia-genre-maest")
                && !rows.Any(r => r.Analyzer == "essentia-genre-effnet")
                && rows.Any(r => r.Analyzer == "essentia-mood")),
            Arg.Any<CancellationToken>());
        await fileTags.Received(1).ApplyAutoTagsAsync(
            FileId,
            Arg.Any<IReadOnlyCollection<TagCandidate>>(),
            false,
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SyncFileAsync_falls_back_when_higher_priority_is_failure()
    {
        var (service, uow, derivation, settings, fileTags) = CreateService();
        uow.Files.GetByIdAsync(FileId, Arg.Any<CancellationToken>())
            .Returns(File());
        uow.FileEnrichments.FindAsync(Arg.Any<Expression<Func<FileEnrichment, bool>>>(), Arg.Any<CancellationToken>())
            .Returns(
            [
                GenreRow("essentia-genre-maest", """{"success":false,"error":"boom"}"""),
                GenreRow("essentia-genre-effnet", """{"backbone":"effnet","predictions":[]}"""),
                MoodRow(),
            ]);
        derivation.DeriveAsync(Arg.Any<IReadOnlyCollection<FileEnrichment>>(), Arg.Any<CancellationToken>())
            .Returns([Candidate(GenreTagId, 0.5, TagFacet.Genre)]);
        settings.GetBehaviorAsync(OwnerId, Arg.Any<CancellationToken>())
            .Returns(new BehaviorSettingsValue());

        await service.SyncFileAsync(FileId, CancellationToken.None);

        await derivation.Received(1).DeriveAsync(
            Arg.Is<IReadOnlyCollection<FileEnrichment>>(rows =>
                rows.Count == 2
                && !rows.Any(r => r.Analyzer == "essentia-genre-maest")
                && rows.Any(r => r.Analyzer == "essentia-genre-effnet")),
            Arg.Any<CancellationToken>());
        await fileTags.Received(1).ApplyAutoTagsAsync(
            FileId,
            Arg.Any<IReadOnlyCollection<TagCandidate>>(),
            false,
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SyncFileAsync_no_authoritative_rows_does_not_apply()
    {
        var (service, uow, derivation, _, fileTags) = CreateService();
        uow.Files.GetByIdAsync(FileId, Arg.Any<CancellationToken>())
            .Returns(File());
        uow.FileEnrichments.FindAsync(Arg.Any<Expression<Func<FileEnrichment, bool>>>(), Arg.Any<CancellationToken>())
            .Returns(
            [
                GenreRow("essentia-genre-maest", """{"success":false,"error":"boom"}"""),
                MoodRow("""{"success":false,"error":"boom"}"""),
            ]);

        await service.SyncFileAsync(FileId, CancellationToken.None);

        await derivation.DidNotReceiveWithAnyArgs().DeriveAsync(default, default);
        await fileTags.DidNotReceiveWithAnyArgs().ApplyAutoTagsAsync(default, default, default, default);
    }

    [Fact]
    public async Task SyncFileAsync_deleted_file_does_not_apply()
    {
        var (service, uow, derivation, _, fileTags) = CreateService();
        var file = File();
        file.DeletedAt = DateTime.UtcNow;
        uow.Files.GetByIdAsync(FileId, Arg.Any<CancellationToken>())
            .Returns(file);

        await service.SyncFileAsync(FileId, CancellationToken.None);

        await derivation.DidNotReceiveWithAnyArgs().DeriveAsync(default, default);
        await fileTags.DidNotReceiveWithAnyArgs().ApplyAutoTagsAsync(default, default, default, default);
    }

    [Fact]
    public async Task SyncFileAsync_passes_owner_allow_auto_tag_regression()
    {
        var (service, uow, derivation, settings, fileTags) = CreateService();
        uow.Files.GetByIdAsync(FileId, Arg.Any<CancellationToken>())
            .Returns(File());
        uow.FileEnrichments.FindAsync(Arg.Any<Expression<Func<FileEnrichment, bool>>>(), Arg.Any<CancellationToken>())
            .Returns([MoodRow()]);
        derivation.DeriveAsync(Arg.Any<IReadOnlyCollection<FileEnrichment>>(), Arg.Any<CancellationToken>())
            .Returns([Candidate(MoodTagId, 0.7, TagFacet.Mood)]);
        settings.GetBehaviorAsync(OwnerId, Arg.Any<CancellationToken>())
            .Returns(new BehaviorSettingsValue { AllowAutoTagRegression = true });

        await service.SyncFileAsync(FileId, CancellationToken.None);

        await settings.Received(1).GetBehaviorAsync(OwnerId, Arg.Any<CancellationToken>());
        await fileTags.Received(1).ApplyAutoTagsAsync(
            FileId,
            Arg.Any<IReadOnlyCollection<TagCandidate>>(),
            true,
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SyncFileAsync_no_candidates_does_not_apply()
    {
        var (service, uow, derivation, _, fileTags) = CreateService();
        uow.Files.GetByIdAsync(FileId, Arg.Any<CancellationToken>())
            .Returns(File());
        uow.FileEnrichments.FindAsync(Arg.Any<Expression<Func<FileEnrichment, bool>>>(), Arg.Any<CancellationToken>())
            .Returns([MoodRow()]);
        derivation.DeriveAsync(Arg.Any<IReadOnlyCollection<FileEnrichment>>(), Arg.Any<CancellationToken>())
            .Returns([]);

        await service.SyncFileAsync(FileId, CancellationToken.None);

        await fileTags.DidNotReceiveWithAnyArgs().ApplyAutoTagsAsync(default, default, default, default);
    }

    private static (AutoTagSyncService Service,
        IUnitOfWork UnitOfWork,
        IAutoTagDerivationService Derivation,
        IUserSettingsService Settings,
        IFileTagService FileTags) CreateService()
    {
        var unitOfWork = Substitute.For<IUnitOfWork>();
        unitOfWork.Files.Returns(Substitute.For<IFileRepository>());
        unitOfWork.FileEnrichments.Returns(Substitute.For<IFileEnrichmentRepository>());

        var derivation = Substitute.For<IAutoTagDerivationService>();
        var settings = Substitute.For<IUserSettingsService>();
        var fileTags = Substitute.For<IFileTagService>();

        var service = new AutoTagSyncService(
            unitOfWork,
            Options.Create(new AutoTaggingOptions()),
            settings,
            derivation,
            fileTags,
            NullLogger<AutoTagSyncService>.Instance);

        return (service, unitOfWork, derivation, settings, fileTags);
    }

    private static File File() => new()
    {
        Id = FileId,
        OwnerId = OwnerId,
        Name = "song.mp3",
        MimeType = "audio/mpeg",
        NormalizedName = "song.mp3",
        SearchVector = null!,
    };

    private static FileEnrichment GenreRow(string analyzer, string payload) => new()
    {
        Id = Guid.NewGuid(),
        FileId = FileId,
        Analyzer = analyzer,
        Version = "1.0",
        PayloadJson = payload,
    };

    private static FileEnrichment MoodRow(string? payload = null) => new()
    {
        Id = Guid.NewGuid(),
        FileId = FileId,
        Analyzer = "essentia-mood",
        Version = "1.0",
        PayloadJson = payload ?? """{"mood_aggressive":{"not_aggressive":0.2,"aggressive":0.8}}""",
    };

    private static TagCandidate Candidate(Guid tagId, double confidence, TagFacet facet)
        => new(tagId, confidence, facet);
}