using System.Text;
using Alexandria.Common;
using Alexandria.Common.Repositories;
using Alexandria.Common.Services;
using Alexandria.Data.Models;
using Alexandria.Dto.Enrichment;
using Alexandria.Services.Storage.Policies;
using AwesomeAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Directory = Alexandria.Data.Models.Directory;

namespace Alexandria.Tests.Unit.AutoTagging;

public class EnrichmentBackfillServiceTests
{
    private static readonly Guid PolicyId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid RootDir = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid ChildDir = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid NewFile = Guid.Parse("33333333-3333-3333-3333-333333333333");
    private static readonly Guid EnrichedFile = Guid.Parse("44444444-4444-4444-4444-444444444444");
    private static readonly Guid PdfFile = Guid.Parse("55555555-5555-5555-5555-555555555555");

    private const string AudioMime = "audio/mpeg";

    private static readonly DateTime PolicyCreatedAt = new(2026, 8, 1, 12, 0, 0, DateTimeKind.Utc);

    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly IPublisherService _publisher = Substitute.For<IPublisherService>();
    private readonly IDirectoryPolicyRepository _policies = Substitute.For<IDirectoryPolicyRepository>();
    private readonly IDirectoryRepository _directories = Substitute.For<IDirectoryRepository>();
    private readonly IEssentiaBatchFileRepository _batchFiles = Substitute.For<IEssentiaBatchFileRepository>();
    private readonly IFileEnrichmentRepository _enrichments = Substitute.For<IFileEnrichmentRepository>();

    public EnrichmentBackfillServiceTests()
    {
        _unitOfWork.DirectoryPolicies.Returns(_policies);
        _unitOfWork.Directories.Returns(_directories);
        _unitOfWork.EssentiaBatchFiles.Returns(_batchFiles);
        _unitOfWork.FileEnrichments.Returns(_enrichments);
    }

    private EnrichmentBackfillService CreateSut(bool enabled = true)
    {
        var configuration = Substitute.For<IConfiguration>();
        configuration["Features:Autotagging"].Returns(enabled ? "true" : "false");
        return new EnrichmentBackfillService(
            _unitOfWork, _publisher, configuration,
            Substitute.For<ILogger<EnrichmentBackfillService>>());
    }

    private void GivenPolicy(bool inheritedByChildren = true)
    {
        _policies.GetByIdAsync(PolicyId, Arg.Any<CancellationToken>())
            .Returns(new DirectoryPolicy
            {
                Id = PolicyId,
                DirectoryId = RootDir,
                InheritedByChildren = inheritedByChildren,
                CreatedAt = PolicyCreatedAt,
            });
        _directories.GetAllSubDirectoriesAsync(RootDir, Arg.Any<CancellationToken>())
            .Returns(new List<Directory>
            {
                new() { Id = ChildDir, Name = "child", ParentId = RootDir },
            });
    }

    [Fact]
    public async Task BackfillPolicyAsync_missing_policy_returns_empty_without_publish()
    {
        _policies.GetByIdAsync(PolicyId, Arg.Any<CancellationToken>()).Returns((DirectoryPolicy?)null);

        var result = await CreateSut().BackfillPolicyAsync(PolicyId, TestContext.Current.CancellationToken);

        result.Should().Be(new EnrichmentBackfillResult(0, 0, 0));
        await _publisher.DidNotReceive().PublishAsync(Arg.Any<byte[]>(), Arg.Any<string>());
        await _batchFiles.DidNotReceive().GetEnrichmentBackfillCandidatesAsync(
            Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<DateTime>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task BackfillPolicyAsync_flag_off_returns_empty_without_publish()
    {
        GivenPolicy();

        var result = await CreateSut(enabled: false)
            .BackfillPolicyAsync(PolicyId, TestContext.Current.CancellationToken);

        result.Should().Be(new EnrichmentBackfillResult(0, 0, 0));
        await _publisher.DidNotReceive().PublishAsync(Arg.Any<byte[]>(), Arg.Any<string>());
    }

    [Fact]
    public async Task BackfillPolicyAsync_publishes_only_supported_unenriched_files()
    {
        GivenPolicy();
        _batchFiles.GetEnrichmentBackfillCandidatesAsync(
                Arg.Any<IReadOnlyCollection<Guid>>(), PolicyCreatedAt, Arg.Any<CancellationToken>())
            .Returns(new List<EnrichmentBackfillCandidate>
            {
                new(NewFile, AudioMime),
                new(EnrichedFile, AudioMime),
                new(PdfFile, "application/pdf"),
            });
        _enrichments.HasSuccessfulAutoTagEnrichmentAsync(NewFile, Arg.Any<CancellationToken>())
            .Returns(false);
        _enrichments.HasSuccessfulAutoTagEnrichmentAsync(EnrichedFile, Arg.Any<CancellationToken>())
            .Returns(true);

        var result = await CreateSut().BackfillPolicyAsync(PolicyId, TestContext.Current.CancellationToken);

        result.Should().Be(new EnrichmentBackfillResult(3, 1, 2));
        await _publisher.Received(1).PublishAsync(
            Arg.Is<byte[]>(body => Encoding.UTF8.GetString(body) == NewFile.ToString()),
            AutoTagTrigger.EnrichRoutingKey);
    }

    [Fact]
    public async Task BackfillPolicyAsync_recursive_policy_includes_subtree_and_cutoff()
    {
        GivenPolicy(inheritedByChildren: true);
        _batchFiles.GetEnrichmentBackfillCandidatesAsync(
                Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<DateTime>(), Arg.Any<CancellationToken>())
            .Returns(new List<EnrichmentBackfillCandidate>());

        await CreateSut().BackfillPolicyAsync(PolicyId, TestContext.Current.CancellationToken);

        await _directories.Received(1).GetAllSubDirectoriesAsync(RootDir, Arg.Any<CancellationToken>());
        await _batchFiles.Received(1).GetEnrichmentBackfillCandidatesAsync(
            Arg.Is<IReadOnlyCollection<Guid>>(ids => ids.Contains(RootDir)),
            PolicyCreatedAt,
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task BackfillPolicyAsync_non_recursive_policy_skips_subtree_lookup()
    {
        GivenPolicy(inheritedByChildren: false);
        _batchFiles.GetEnrichmentBackfillCandidatesAsync(
                Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<DateTime>(), Arg.Any<CancellationToken>())
            .Returns(new List<EnrichmentBackfillCandidate>());

        await CreateSut().BackfillPolicyAsync(PolicyId, TestContext.Current.CancellationToken);

        await _directories.DidNotReceive().GetAllSubDirectoriesAsync(
            Arg.Any<Guid>(), Arg.Any<CancellationToken>());
        await _batchFiles.Received(1).GetEnrichmentBackfillCandidatesAsync(
            Arg.Is<IReadOnlyCollection<Guid>>(ids => ids.Count == 1 && ids.Contains(RootDir)),
            PolicyCreatedAt,
            Arg.Any<CancellationToken>());
    }
}