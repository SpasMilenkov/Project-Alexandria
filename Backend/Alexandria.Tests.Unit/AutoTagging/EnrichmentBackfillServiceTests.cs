using System.Text;
using Alexandria.Common;
using Alexandria.Common.Policies;
using Alexandria.Common.Repositories;
using Alexandria.Common.Services;
using Alexandria.Data.Models;
using Alexandria.Data.Models.Enumerators;
using Alexandria.Dto.Enrichment;
using Alexandria.Services.Storage.Policies;
using AwesomeAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Directory = Alexandria.Data.Models.Directory;
using File = Alexandria.Data.Models.File;

namespace Alexandria.Tests.Unit.AutoTagging;

public class EnrichmentBackfillServiceTests
{
    private static readonly Guid PolicyId = Guid.NewGuid();
    private static readonly Guid RuleId = Guid.NewGuid();
    private static readonly Guid RootDir = Guid.NewGuid();
    private static readonly Guid ChildDir = Guid.NewGuid();
    private static readonly DateTime PolicyCreatedAt = new(2026, 10, 1, 12, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime RuleCreatedAt = PolicyCreatedAt.AddHours(2);
    private static readonly DateTime RuleUpdatedAt = RuleCreatedAt.AddHours(2);

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

        return new EnrichmentBackfillService(_unitOfWork, _publisher, configuration,
            Substitute.For<ILogger<EnrichmentBackfillService>>());
    }

    private DirectoryPolicy GivenPolicy(bool inherited = false)
    {
        var policy = new DirectoryPolicy
        {
            Id = PolicyId,
            DirectoryId = RootDir,
            InheritedByChildren = inherited,
            CreatedAt = PolicyCreatedAt,
            Rules = [new PolicyRule
            {
                Id = RuleId,
                PolicyId = PolicyId,
                ActionType = PolicyActionType.AutoTag,
                TriggerType = PolicyTriggerType.AnyFile,
                CreatedAt = RuleCreatedAt,
                UpdatedAt = RuleUpdatedAt
            }]
        };

        _policies.GetByIdAsync(PolicyId, Arg.Any<CancellationToken>()).Returns(policy);
        _directories.GetAllSubDirectoriesAsync(RootDir, Arg.Any<CancellationToken>())
            .Returns(new List<Directory> { new() { Id = ChildDir, Name = "child", ParentId = RootDir } });

        return policy;
    }

    private static EnrichmentBackfillRequest Request(DateTime cutoff) =>
        new(PolicyId, RuleId, new DateTimeOffset(cutoff));

    [Fact]
    public async Task BackfillRuleAsync_missing_policy_returns_empty()
    {
        var result = await CreateSut().BackfillRuleAsync(Request(RuleCreatedAt),
            TestContext.Current.CancellationToken);

        result.Should().Be(new EnrichmentBackfillResult(0, 0, 0));
        await _publisher.DidNotReceive().PublishAsync(Arg.Any<byte[]>(), Arg.Any<string>());
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("deleted")]
    [InlineData("transcode")]
    public async Task BackfillRuleAsync_no_live_autotag_rule_does_not_scan_or_publish(string state)
    {
        var policy = GivenPolicy();
        var rule = policy.Rules.Single();

        if (state == "missing")
            policy.Rules.Clear();
        else if (state == "deleted")
            rule.DeletedAt = RuleUpdatedAt;
        else
            rule.ActionType = PolicyActionType.Transcode;

        var result = await CreateSut().BackfillRuleAsync(Request(RuleCreatedAt),
            TestContext.Current.CancellationToken);

        result.Should().Be(new EnrichmentBackfillResult(0, 0, 0));
        await _batchFiles.DidNotReceive().GetEnrichmentBackfillCandidatesAsync(
            Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<DateTime>(), Arg.Any<CancellationToken>());
        await _publisher.DidNotReceive().PublishAsync(Arg.Any<byte[]>(), Arg.Any<string>());
    }

    [Fact]
    public async Task BackfillRuleAsync_autotagging_disabled_does_not_publish()
    {
        GivenPolicy();

        var result = await CreateSut(enabled: false).BackfillRuleAsync(Request(RuleCreatedAt),
            TestContext.Current.CancellationToken);

        result.Should().Be(new EnrichmentBackfillResult(0, 0, 0));
        await _publisher.DidNotReceive().PublishAsync(Arg.Any<byte[]>(), Arg.Any<string>());
    }

    [Theory]
    [InlineData(false, FileGroups.Audio, 1)]
    [InlineData(true, FileGroups.Audio, 2)]
    [InlineData(true, FileGroups.Images, 0)]
    [InlineData(true, FileGroups.Documents, 0)]
    public async Task BackfillRuleAsync_rule_change_backfills_only_matching_supported_unenriched_files(
        bool updated, string groups, int expectedPublished)
    {
        var policy = GivenPolicy();
        var rule = policy.Rules.Single();
        rule.TriggerType = PolicyTriggerType.FileGroup;
        rule.TriggerValue = groups;
        var cutoff = RuleCreatedAt;

        if (updated)
            cutoff = RuleUpdatedAt;

        var gapFile = new File
        {
            Id = Guid.NewGuid(), Name = "gap.mp3", MimeType = "audio/mpeg", DirectoryId = RootDir,
            CreatedAt = PolicyCreatedAt.AddHours(1)
        };
        var laterFile = new File
        {
            Id = Guid.NewGuid(), Name = "later.mp3", MimeType = "audio/mpeg", DirectoryId = RootDir,
            CreatedAt = RuleCreatedAt.AddHours(1)
        };
        var enriched = new File
        {
            Id = Guid.NewGuid(), Name = "enriched.mp3", MimeType = "audio/mpeg", DirectoryId = RootDir,
            CreatedAt = PolicyCreatedAt
        };
        var pdf = new File
        {
            Id = Guid.NewGuid(), Name = "doc.pdf", MimeType = "application/pdf", DirectoryId = RootDir,
            CreatedAt = PolicyCreatedAt
        };
        var files = new[] { gapFile, laterFile, enriched, pdf };

        _batchFiles.GetEnrichmentBackfillCandidatesAsync(
                Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<DateTime>(), Arg.Any<CancellationToken>())
            .Returns(call => files.Where(EnrichmentBackfill.BuildScopeFilter(
                    call.Arg<IReadOnlyCollection<Guid>>(), call.Arg<DateTime>()).Compile())
                .Select(f => new EnrichmentBackfillCandidate(f.Id, f.MimeType)).ToList());
        _enrichments.HasSuccessfulAutoTagEnrichmentAsync(enriched.Id, Arg.Any<CancellationToken>())
            .Returns(true);

        var result = await CreateSut().BackfillRuleAsync(Request(cutoff), TestContext.Current.CancellationToken);

        result.Published.Should().Be(expectedPublished);
        await _batchFiles.Received(1).GetEnrichmentBackfillCandidatesAsync(
            Arg.Any<IReadOnlyCollection<Guid>>(), cutoff, Arg.Any<CancellationToken>());
        var publishedIds = _publisher.ReceivedCalls()
            .Select(call => Guid.Parse(Encoding.UTF8.GetString((byte[])call.GetArguments()[0]!))).ToList();

        var expectedIds = new List<Guid>();

        if (expectedPublished > 0)
            expectedIds.Add(gapFile.Id);

        if (expectedPublished > 1)
            expectedIds.Add(laterFile.Id);

        publishedIds.Should().BeEquivalentTo(expectedIds);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task BackfillRuleAsync_inheritance_controls_directory_scope(bool inherited)
    {
        GivenPolicy(inherited);

        await CreateSut().BackfillRuleAsync(Request(RuleCreatedAt), TestContext.Current.CancellationToken);

        var expectedDirectories = new List<Guid> { RootDir };

        if (inherited)
            expectedDirectories.Add(ChildDir);

        await _batchFiles.Received(1).GetEnrichmentBackfillCandidatesAsync(
            Arg.Is<IReadOnlyCollection<Guid>>(ids => ids.Count == expectedDirectories.Count
                && expectedDirectories.All(ids.Contains)), RuleCreatedAt, Arg.Any<CancellationToken>());
        await _directories.Received(inherited ? 1 : 0)
            .GetAllSubDirectoriesAsync(RootDir, Arg.Any<CancellationToken>());
    }
}
