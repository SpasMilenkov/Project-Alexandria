using System.Linq.Expressions;
using Alexandria.Common;
using Alexandria.Common.Repositories;
using Alexandria.Data.Models;
using Alexandria.Data.Models.Enumerators;
using Alexandria.Services.Storage.AutoTagging;
using AwesomeAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace Alexandria.Tests.Unit.AutoTagging;

public class AutoTagDerivationServiceTests
{
    private static readonly Guid RockId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid NuMetalId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid AggressiveId = Guid.Parse("33333333-3333-3333-3333-333333333333");

    [Fact]
    public async Task DeriveAsync_genre_payload_returns_candidates()
    {
        var (service, repo) = CreateService();
        repo.FindAsync(Arg.Any<Expression<Func<Tag, bool>>>(), Arg.Any<CancellationToken>())
            .Returns(BuildTaxonomy());

        var rows = new List<FileEnrichment>
        {
            GenreRow(
                """{"backbone":"effnet","predictions":[{"label":"Rock---Nu Metal","score":0.8}]}"""),
        };

        var candidates = await service.DeriveAsync(rows, CancellationToken.None);

        candidates.Should().ContainSingle(c => c.TagId == NuMetalId && c.Confidence == 0.8);
    }

    [Fact]
    public async Task DeriveAsync_genre_failure_payload_is_skipped()
    {
        var (service, repo) = CreateService();
        repo.FindAsync(Arg.Any<Expression<Func<Tag, bool>>>(), Arg.Any<CancellationToken>())
            .Returns(BuildTaxonomy());

        var rows = new List<FileEnrichment>
        {
            GenreRow("""{"success":false,"error":"boom"}"""),
        };

        var candidates = await service.DeriveAsync(rows, CancellationToken.None);

        candidates.Should().BeEmpty();
    }

    [Fact]
    public async Task DeriveAsync_mood_failure_payload_is_skipped()
    {
        var (service, repo) = CreateService();
        repo.FindAsync(Arg.Any<Expression<Func<Tag, bool>>>(), Arg.Any<CancellationToken>())
            .Returns(BuildTaxonomy());

        var rows = new List<FileEnrichment>
        {
            MoodRow("""{"success":false,"error":"boom"}"""),
        };

        var candidates = await service.DeriveAsync(rows, CancellationToken.None);

        candidates.Should().BeEmpty();
    }

    [Fact]
    public async Task DeriveAsync_mood_payload_returns_candidates()
    {
        var (service, repo) = CreateService();
        repo.FindAsync(Arg.Any<Expression<Func<Tag, bool>>>(), Arg.Any<CancellationToken>())
            .Returns(BuildTaxonomy());

        var rows = new List<FileEnrichment>
        {
            MoodRow("""{"mood_aggressive":{"not_aggressive":0.2,"aggressive":0.8}}"""),
        };

        var candidates = await service.DeriveAsync(rows, CancellationToken.None);

        candidates.Should().ContainSingle(c => c.TagId == AggressiveId && c.Confidence == 0.8);
    }

    [Fact]
    public async Task DeriveAsync_unknown_analyzer_is_ignored()
    {
        var (service, repo) = CreateService();
        repo.FindAsync(Arg.Any<Expression<Func<Tag, bool>>>(), Arg.Any<CancellationToken>())
            .Returns(BuildTaxonomy());

        var rows = new List<FileEnrichment>
        {
            new()
            {
                Id = Guid.NewGuid(),
                FileId = Guid.NewGuid(),
                Analyzer = "essentia-embedding",
                Version = "1.0",
                PayloadJson = "{}",
            },
        };

        var candidates = await service.DeriveAsync(rows, CancellationToken.None);

        candidates.Should().BeEmpty();
    }

    [Fact]
    public async Task DeriveAsync_deduplicates_by_tag_keeping_max_confidence()
    {
        var (service, repo) = CreateService();
        repo.FindAsync(Arg.Any<Expression<Func<Tag, bool>>>(), Arg.Any<CancellationToken>())
            .Returns(BuildTaxonomy());

        // Two genre rows (e.g. effnet + maest) both match the same child.
        var rows = new List<FileEnrichment>
        {
            GenreRow(
                """{"backbone":"effnet","predictions":[{"label":"Rock---Nu Metal","score":0.5}]}"""),
            GenreRow(
                """{"backbone":"maest","predictions":[{"label":"Rock---Nu Metal","score":0.9}]}"""),
        };

        var candidates = await service.DeriveAsync(rows, CancellationToken.None);

        candidates.Should().ContainSingle(c => c.TagId == NuMetalId);
        candidates.Single(c => c.TagId == NuMetalId).Confidence.Should().Be(0.9);
    }

    [Fact]
    public async Task DeriveAsync_empty_rows_return_empty()
    {
        var (service, _) = CreateService();

        var candidates = await service.DeriveAsync([], CancellationToken.None);

        candidates.Should().BeEmpty();
    }

    private static (AutoTagDerivationService Service, ITagRepository TagRepository) CreateService()
    {
        var unitOfWork = Substitute.For<IUnitOfWork>();
        var tagRepository = Substitute.For<ITagRepository>();
        unitOfWork.Tags.Returns(tagRepository);

        var options = Options.Create(new AutoTaggingOptions());
        var service = new AutoTagDerivationService(
            unitOfWork,
            options,
            NullLogger<AutoTagDerivationService>.Instance);

        return (service, tagRepository);
    }

    private static List<Tag> BuildTaxonomy() =>
    [
        new Tag
        {
            Id = RockId,
            Name = "Rock",
            Icon = "tag",
            Color = "#EF4444",
            ExternalKey = "Rock",
            Facet = TagFacet.Genre,
        },
        new Tag
        {
            Id = NuMetalId,
            Name = "Nu Metal",
            Icon = "tag",
            Color = "#EF4444",
            ExternalKey = "Rock---Nu Metal",
            Facet = TagFacet.Genre,
            ParentId = RockId,
        },
        new Tag
        {
            Id = AggressiveId,
            Name = "Aggressive",
            Icon = "alert",
            Color = "#EF4444",
            ExternalKey = "mood_aggressive:aggressive",
            Facet = TagFacet.Mood,
        },
    ];

    private static FileEnrichment GenreRow(string payload) => new()
    {
        Id = Guid.NewGuid(),
        FileId = Guid.NewGuid(),
        Analyzer = "essentia-genre-effnet",
        Version = "1.0",
        PayloadJson = payload,
    };

    private static FileEnrichment MoodRow(string payload) => new()
    {
        Id = Guid.NewGuid(),
        FileId = Guid.NewGuid(),
        Analyzer = "essentia-mood",
        Version = "1.0",
        PayloadJson = payload,
    };
}