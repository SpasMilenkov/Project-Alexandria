using Alexandria.Common;
using Alexandria.Common.Repositories;
using Alexandria.Data.Models;
using Alexandria.Data.Models.Enumerators;
using Alexandria.Dto.Autotag;
using Alexandria.Services.Storage;
using AwesomeAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using File = Alexandria.Data.Models.File;

namespace Alexandria.Tests.Unit.AutoTagging;

public class FileTagServiceApplyAutoTagsTests
{
    private static readonly Guid NuMetalId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid RockId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid AggressiveId = Guid.Parse("33333333-3333-3333-3333-333333333333");
    private static readonly Guid MoodId = Guid.Parse("44444444-4444-4444-4444-444444444444");

    // ---- Insert ----

    [Fact]
    public async Task Apply_no_existing_row_inserts_auto()
    {
        var file = CreateFile([]);
        var service = CreateService(file);

        await service.ApplyAutoTagsAsync(file.Id,
            [Cand(NuMetalId, 0.8, TagFacet.Genre), Cand(RockId, 0.8, TagFacet.Genre)],
            allowAutoTagRegression: false);

        file.FileTags.Should().HaveCount(2);
        file.FileTags.Should().AllSatisfy(ft =>
        {
            ft.Source.Should().Be(TagSource.Auto);
            ft.Confidence.Should().Be(0.8);
        });
        file.FileTags.Should().Contain(ft => ft.TagId == NuMetalId);
        file.FileTags.Should().Contain(ft => ft.TagId == RockId);
    }

    // ---- Existing Auto + regression rule ----

    [Fact]
    public async Task Apply_existing_auto_keeps_higher_confidence_by_default()
    {
        var file = CreateFile([(NuMetalId, TagSource.Auto, TagFacet.Genre, 0.6)]);
        var service = CreateService(file);

        await service.ApplyAutoTagsAsync(file.Id, [Cand(NuMetalId, 0.4, TagFacet.Genre)],
            allowAutoTagRegression: false);

        file.FileTags.Single().Confidence.Should().Be(0.6);
    }

    [Fact]
    public async Task Apply_existing_auto_updates_to_higher_confidence()
    {
        var file = CreateFile([(NuMetalId, TagSource.Auto, TagFacet.Genre, 0.4)]);
        var service = CreateService(file);

        await service.ApplyAutoTagsAsync(file.Id, [Cand(NuMetalId, 0.7, TagFacet.Genre)],
            allowAutoTagRegression: false);

        file.FileTags.Single().Confidence.Should().Be(0.7);
    }

    [Fact]
    public async Task Apply_existing_auto_regression_allowed_takes_latest()
    {
        var file = CreateFile([(NuMetalId, TagSource.Auto, TagFacet.Genre, 0.6)]);
        var service = CreateService(file);

        await service.ApplyAutoTagsAsync(file.Id, [Cand(NuMetalId, 0.4, TagFacet.Genre)],
            allowAutoTagRegression: true);

        file.FileTags.Single().Confidence.Should().Be(0.4);
    }

    // ---- User / Suppressed precedence ----

    [Fact]
    public async Task Apply_user_row_is_never_touched()
    {
        var file = CreateFile([(NuMetalId, TagSource.User, TagFacet.Genre, null)]);
        var service = CreateService(file);

        await service.ApplyAutoTagsAsync(file.Id, [Cand(NuMetalId, 0.9, TagFacet.Genre)],
            allowAutoTagRegression: true);

        var ft = file.FileTags.Single();
        ft.Source.Should().Be(TagSource.User);
        ft.Confidence.Should().BeNull();
    }

    [Fact]
    public async Task Apply_suppressed_row_is_never_touched()
    {
        var file = CreateFile([(NuMetalId, TagSource.Suppressed, TagFacet.Genre, null)]);
        var service = CreateService(file);

        await service.ApplyAutoTagsAsync(file.Id, [Cand(NuMetalId, 0.9, TagFacet.Genre)],
            allowAutoTagRegression: true);

        var ft = file.FileTags.Single();
        ft.Source.Should().Be(TagSource.Suppressed);
    }

    [Fact]
    public async Task Apply_filename_row_matching_candidate_is_promoted_to_auto()
    {
        var file = CreateFile([(NuMetalId, TagSource.FileName, TagFacet.Genre, null)]);
        var service = CreateService(file);

        await service.ApplyAutoTagsAsync(file.Id, [Cand(NuMetalId, 0.9, TagFacet.Genre)],
            allowAutoTagRegression: false);

        var ft = file.FileTags.Single();
        ft.Source.Should().Be(TagSource.Auto);
        ft.Confidence.Should().Be(0.9);
    }

    [Fact]
    public async Task Apply_filemetadata_row_matching_candidate_is_promoted_to_auto()
    {
        var file = CreateFile([(NuMetalId, TagSource.FileMetadata, TagFacet.Genre, null)]);
        var service = CreateService(file);

        await service.ApplyAutoTagsAsync(file.Id, [Cand(NuMetalId, 0.9, TagFacet.Genre)],
            allowAutoTagRegression: false);

        var ft = file.FileTags.Single();
        ft.Source.Should().Be(TagSource.Auto);
        ft.Confidence.Should().Be(0.9);
    }

    // ---- Prune-to-candidate-set ----

    [Fact]
    public async Task Apply_prunes_stale_auto_rows_in_candidate_facet()
    {
        var file = CreateFile([(RockId, TagSource.Auto, TagFacet.Genre, 0.3)]);
        var service = CreateService(file);

        await service.ApplyAutoTagsAsync(file.Id, [Cand(NuMetalId, 0.8, TagFacet.Genre)],
            allowAutoTagRegression: false);

        file.FileTags.Should().ContainSingle();
        file.FileTags.Single().TagId.Should().Be(NuMetalId);
    }

    [Fact]
    public async Task Apply_no_candidates_prunes_nothing()
    {
        var file = CreateFile([(RockId, TagSource.Auto, TagFacet.Genre, 0.3)]);
        var service = CreateService(file);

        await service.ApplyAutoTagsAsync(file.Id, [], allowAutoTagRegression: false);

        file.FileTags.Should().ContainSingle(ft => ft.TagId == RockId);
    }

    [Fact]
    public async Task Apply_genre_sync_leaves_mood_tags_alone()
    {
        var file = CreateFile(
        [
            (RockId, TagSource.Auto, TagFacet.Genre, 0.3),
            (MoodId, TagSource.Auto, TagFacet.Mood, 0.8),
        ]);
        var service = CreateService(file);

        await service.ApplyAutoTagsAsync(file.Id, [Cand(NuMetalId, 0.8, TagFacet.Genre)],
            allowAutoTagRegression: false);

        file.FileTags.Should().HaveCount(2);
        file.FileTags.Should().Contain(ft => ft.TagId == NuMetalId);
        file.FileTags.Should().Contain(ft => ft.TagId == MoodId);
        file.FileTags.Should().NotContain(ft => ft.TagId == RockId);
    }

    [Fact]
    public async Task Apply_prune_spares_user_rows()
    {
        var file = CreateFile(
        [
            (RockId, TagSource.User, TagFacet.Genre, null),
            (NuMetalId, TagSource.Auto, TagFacet.Genre, 0.8),
        ]);
        var service = CreateService(file);

        await service.ApplyAutoTagsAsync(file.Id, [Cand(AggressiveId, 0.9, TagFacet.Genre)],
            allowAutoTagRegression: false);

        file.FileTags.Should().HaveCount(2);
        file.FileTags.Should().Contain(ft => ft.TagId == RockId && ft.Source == TagSource.User);
        file.FileTags.Should().Contain(ft => ft.TagId == AggressiveId);
    }

    [Fact]
    public async Task Apply_prune_spares_suppressed_rows()
    {
        var file = CreateFile(
        [
            (RockId, TagSource.Suppressed, TagFacet.Genre, null),
            (NuMetalId, TagSource.Auto, TagFacet.Genre, 0.8),
        ]);
        var service = CreateService(file);

        await service.ApplyAutoTagsAsync(file.Id, [Cand(AggressiveId, 0.9, TagFacet.Genre)],
            allowAutoTagRegression: false);

        file.FileTags.Should().HaveCount(2);
        file.FileTags.Should().Contain(ft => ft.TagId == RockId && ft.Source == TagSource.Suppressed);
        file.FileTags.Should().Contain(ft => ft.TagId == AggressiveId);
    }

    [Fact]
    public async Task Apply_prune_removes_only_auto_rows_not_in_candidates()
    {
        // Auto row for a candidate that is *not* updated (lower confidence, no regression)
        // must survive the prune because its tag is in the candidate set.
        var file = CreateFile([(NuMetalId, TagSource.Auto, TagFacet.Genre, 0.9)]);
        var service = CreateService(file);

        await service.ApplyAutoTagsAsync(file.Id, [Cand(NuMetalId, 0.4, TagFacet.Genre)],
            allowAutoTagRegression: false);

        file.FileTags.Should().ContainSingle(ft => ft.TagId == NuMetalId && ft.Confidence == 0.9);
    }

    [Fact]
    public async Task Apply_missing_file_throws()
    {
        var unitOfWork = Substitute.For<IUnitOfWork>();
        var fileRepo = Substitute.For<IFileRepository>();
        unitOfWork.Files.Returns(fileRepo);
        var service = new FileTagService(unitOfWork, NullLogger<FileTagService>.Instance);

        var act = async () => await service.ApplyAutoTagsAsync(Guid.NewGuid(),
            [Cand(NuMetalId, 0.8, TagFacet.Genre)], allowAutoTagRegression: false);

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    private static FileTagService CreateService(File file)
    {
        var unitOfWork = Substitute.For<IUnitOfWork>();
        var fileRepo = Substitute.For<IFileRepository>();
        unitOfWork.Files.Returns(fileRepo);
        fileRepo.GetFileEntityWithTagsAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(file);

        return new FileTagService(unitOfWork, NullLogger<FileTagService>.Instance);
    }

    private static File CreateFile(
        IEnumerable<(Guid TagId, TagSource Source, TagFacet? Facet, double? Confidence)> tags)
    {
        var file = new File
        {
            Id = Guid.NewGuid(),
            Name = "song.mp3",
            MimeType = "audio/mpeg",
            OwnerId = Guid.NewGuid(),
        };

        foreach (var (tagId, source, facet, confidence) in tags)
        {
            file.FileTags.Add(new FileTag
            {
                Id = Guid.NewGuid(),
                TagId = tagId,
                Source = source,
                Confidence = confidence,
                Tag = new Tag
                {
                    Id = tagId,
                    Name = tagId.ToString(),
                    Icon = "tag",
                    Color = "#000000",
                    Facet = facet,
                },
            });
        }

        return file;
    }

    private static TagCandidate Cand(Guid tagId, double confidence, TagFacet facet) =>
        new(tagId, confidence, facet);
}