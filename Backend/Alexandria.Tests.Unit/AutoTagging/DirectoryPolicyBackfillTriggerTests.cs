using System.Linq.Expressions;
using System.Text.Json;
using Alexandria.Common;
using Alexandria.Common.Config;
using Alexandria.Common.Policies;
using Alexandria.Common.Repositories;
using Alexandria.Common.Services;
using Alexandria.Data.Models;
using Alexandria.Data.Models.Enumerators;
using Alexandria.Dto.Enrichment;
using Alexandria.Dto.Files.Streaming;
using Alexandria.Dto.Policies;
using Alexandria.Services.Storage.Policies;
using AwesomeAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace Alexandria.Tests.Unit.AutoTagging;

public class DirectoryPolicyBackfillTriggerTests
{
    private static readonly Guid PolicyId = Guid.NewGuid();
    private static readonly Guid RuleId = Guid.NewGuid();
    private static readonly Guid DirectoryId = Guid.NewGuid();
    private static readonly Guid OwnerId = Guid.NewGuid();
    private static readonly DateTime CreatedAt = new(2026, 10, 1, 12, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime UpdatedAt = CreatedAt.AddDays(1);
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly IDirectoryPolicyRepository _policies = Substitute.For<IDirectoryPolicyRepository>();
    private readonly IPolicyRuleRepository _rules = Substitute.For<IPolicyRuleRepository>();
    private readonly IOutbox _outbox = Substitute.For<IOutbox>();

    public DirectoryPolicyBackfillTriggerTests()
    {
        _unitOfWork.DirectoryPolicies.Returns(_policies);
        _unitOfWork.PolicyRules.Returns(_rules);

        _policies.ExistsAsync(Arg.Any<Expression<Func<DirectoryPolicy, bool>>>(),
            Arg.Any<CancellationToken>()).Returns(true);

        _rules.AddAsync(Arg.Any<PolicyRule>(), Arg.Any<CancellationToken>()).Returns(call =>
        {
            var rule = call.Arg<PolicyRule>();
            rule.Id = RuleId;
            rule.CreatedAt = CreatedAt;
            return rule;
        });

        _rules.When(r => r.Update(Arg.Any<PolicyRule>()))
            .Do(call => call.Arg<PolicyRule>().UpdatedAt = UpdatedAt);

        _policies.When(p => p.Update(Arg.Any<DirectoryPolicy>()))
            .Do(call => call.Arg<DirectoryPolicy>().UpdatedAt = UpdatedAt);
    }

    private DirectoryPolicyService CreateSut(bool enabled = true, bool audioAnalysis = true, bool streaming = true)
    {
        return new DirectoryPolicyService(_unitOfWork, Substitute.For<IDirectoryService>(),
            _outbox, Substitute.For<ILogger<DirectoryPolicyService>>(),
            Options.Create(new ProcessingCapabilities
            {
                Autotagging = enabled,
                AudioAnalysis = audioAnalysis,
                AdaptiveStreaming = streaming,
            }));
    }

    private static bool MatchesRequest(byte[] body, DateTime cutoff)
    {
        var request = JsonSerializer.Deserialize<EnrichmentBackfillRequest>(body, JsonOptions);

        return request == new EnrichmentBackfillRequest(PolicyId, RuleId, new DateTimeOffset(cutoff));
    }

    private static bool MatchesTranscodeRequest(byte[] body, DateTime cutoff)
    {
        var request = JsonSerializer.Deserialize<TranscodeBackfillRequest>(body, JsonOptions);

        return request == new TranscodeBackfillRequest
        {
            PolicyId = PolicyId,
            RuleId = RuleId,
            Cutoff = new DateTimeOffset(cutoff),
        };
    }

    private static CreatePolicyRuleRequest RuleRequest(PolicyActionType actionType) => new()
    {
        ActionType = actionType,
        TriggerType = PolicyTriggerType.AnyFile,
        TriggerValue = "*",
        Parameters = JsonDocument.Parse("{}")
    };

    private Task NoBackfillEnqueued()
        => _outbox.DidNotReceiveWithAnyArgs().EnqueueAsync(default!, default!, TestContext.Current.CancellationToken);

    private Task BackfillEnqueued(DateTime cutoff)
        => _outbox.Received(1).EnqueueAsync(
            EnrichmentBackfill.BackfillRoutingKey,
            Arg.Is<byte[]>(body => MatchesRequest(body, cutoff)),
            Arg.Any<CancellationToken>());

    private Task TranscodeBackfillEnqueued(DateTime cutoff)
        => _outbox.Received(1).EnqueueAsync(
            TranscodeBackfill.BackfillRoutingKey,
            Arg.Is<byte[]>(body => MatchesTranscodeRequest(body, cutoff)),
            Arg.Any<CancellationToken>());

    // Backup has no job to run yet, so it is the one action without a backfill
    private Task BackfillEnqueuedFor(PolicyActionType actionType, DateTime cutoff)
    {
        if (actionType == PolicyActionType.AutoTag) return BackfillEnqueued(cutoff);
        if (actionType == PolicyActionType.Transcode) return TranscodeBackfillEnqueued(cutoff);

        return NoBackfillEnqueued();
    }

    [Fact]
    public async Task UpdatePolicyAsync_audio_analysis_disabled_preserves_policy_without_backfill_message()
    {
        _policies.GetByIdAsync(PolicyId, Arg.Any<CancellationToken>()).Returns(new DirectoryPolicy
        {
            Id = PolicyId,
            DirectoryId = DirectoryId,
            CreatedAt = CreatedAt,
            Rules =
            [
                new PolicyRule
                {
                    Id = RuleId,
                    ActionType = PolicyActionType.AutoTag,
                    Parameters = JsonDocument.Parse("{}")
                }
            ]
        });

        await CreateSut(audioAnalysis: false).UpdatePolicyAsync(
            PolicyId, inheritedByChildren: true, OwnerId, TestContext.Current.CancellationToken);

        await _unitOfWork.Received(1).SaveChangesAsync(TestContext.Current.CancellationToken);
        await NoBackfillEnqueued();
    }

    [Fact]
    public async Task CreatePolicyAsync_without_autotag_rule_does_not_publish_backfill()
    {
        await CreateSut().CreatePolicyAsync(new CreateDirectoryPolicyRequest { DirectoryId = DirectoryId },
            OwnerId, TestContext.Current.CancellationToken);

        await NoBackfillEnqueued();
    }

    [Fact]
    public async Task CreatePolicyAsync_saves_before_committing_so_an_outer_transaction_can_add_rules()
    {
        await CreateSut().CreatePolicyAsync(new CreateDirectoryPolicyRequest { DirectoryId = DirectoryId },
            OwnerId, TestContext.Current.CancellationToken);

        Received.InOrder(() =>
        {
            _policies.AddAsync(Arg.Any<DirectoryPolicy>(), Arg.Any<CancellationToken>());
            _unitOfWork.SaveChangesAsync(Arg.Any<CancellationToken>());
            _unitOfWork.CommitAsync(Arg.Any<CancellationToken>());
        });
    }

    [Theory]
    [InlineData(PolicyActionType.AutoTag)]
    [InlineData(PolicyActionType.Transcode)]
    [InlineData(PolicyActionType.Backup)]
    public async Task UpdatePolicyAsync_inheritance_change_backfills_each_rule_with_a_job(PolicyActionType actionType)
    {
        _policies.GetByIdAsync(PolicyId, Arg.Any<CancellationToken>()).Returns(new DirectoryPolicy
        {
            Id = PolicyId,
            DirectoryId = DirectoryId,
            CreatedAt = CreatedAt,
            Rules =
            [
                new PolicyRule
                {
                    Id = RuleId,
                    ActionType = actionType,
                    Parameters = JsonDocument.Parse("{}")
                }
            ]
        });

        await CreateSut().UpdatePolicyAsync(PolicyId, true, OwnerId, TestContext.Current.CancellationToken);

        await BackfillEnqueuedFor(actionType, UpdatedAt);
    }

    [Theory]
    [InlineData(PolicyActionType.AutoTag)]
    [InlineData(PolicyActionType.Transcode)]
    [InlineData(PolicyActionType.Backup)]
    public async Task AddRuleAsync_publishes_the_rules_backfill_with_creation_cutoff(PolicyActionType actionType)
    {
        await CreateSut().AddRuleAsync(PolicyId, RuleRequest(actionType), OwnerId,
            TestContext.Current.CancellationToken);

        await BackfillEnqueuedFor(actionType, CreatedAt);
    }

    [Fact]
    public async Task AddRuleAsync_streaming_disabled_does_not_publish_transcode_backfill()
    {
        await CreateSut(streaming: false).AddRuleAsync(PolicyId, RuleRequest(PolicyActionType.Transcode), OwnerId,
            TestContext.Current.CancellationToken);

        await NoBackfillEnqueued();
    }

    [Theory]
    [InlineData(PolicyActionType.AutoTag)]
    [InlineData(PolicyActionType.Transcode)]
    [InlineData(PolicyActionType.Backup)]
    public async Task UpdateRuleAsync_publishes_the_rules_backfill_with_update_cutoff(PolicyActionType actionType)
    {
        _rules.GetByIdAsync(RuleId, Arg.Any<CancellationToken>()).Returns(new PolicyRule
        {
            Id = RuleId,
            PolicyId = PolicyId,
            ActionType = actionType,
            CreatedAt = CreatedAt
        });

        await CreateSut().UpdateRuleAsync(RuleId, new UpdatePolicyRuleRequest
        {
            TriggerType = PolicyTriggerType.AnyFile,
            TriggerValue = "*",
            Parameters = JsonDocument.Parse("{}")
        }, OwnerId, TestContext.Current.CancellationToken);

        await BackfillEnqueuedFor(actionType, UpdatedAt);
    }

    [Fact]
    public async Task AddRuleAsync_autotagging_disabled_does_not_publish()
    {
        await CreateSut(enabled: false).AddRuleAsync(PolicyId, RuleRequest(PolicyActionType.AutoTag), OwnerId,
            TestContext.Current.CancellationToken);

        await NoBackfillEnqueued();
    }

    private static readonly Guid ParentPolicyId = Guid.NewGuid();
    private static readonly Guid ParentRuleId = Guid.NewGuid();

    private void GivenParentFallback()
    {
        _policies.ResolveInheritedPolicyAboveAsync(DirectoryId, Arg.Any<CancellationToken>()).Returns(new DirectoryPolicyDto
        {
            Id = ParentPolicyId,
            DirectoryId = Guid.NewGuid(),
            Rules = [new PolicyRuleDto { Id = ParentRuleId, ActionType = PolicyActionType.Transcode }],
        });
    }

    private void GivenRuleToDelete(bool othersRemain)
    {
        _rules.GetByIdAsync(RuleId, Arg.Any<CancellationToken>()).Returns(new PolicyRule
        {
            Id = RuleId,
            PolicyId = PolicyId,
            ActionType = PolicyActionType.Transcode,
        });

        _rules.ExistsAsync(Arg.Any<Expression<Func<PolicyRule, bool>>>(), Arg.Any<CancellationToken>())
            .Returns(othersRemain);

        _policies.GetByIdAsync(PolicyId, Arg.Any<CancellationToken>())
            .Returns(new DirectoryPolicy { Id = PolicyId, DirectoryId = DirectoryId });
    }

    private static bool IsParentTranscodeBackfill(byte[] body)
    {
        var request = JsonSerializer.Deserialize<TranscodeBackfillRequest>(body, JsonOptions);

        return request is not null && request.PolicyId == ParentPolicyId && request.RuleId == ParentRuleId;
    }

    private Task ParentBackfillEnqueued()
        => _outbox.Received(1).EnqueueAsync(
            TranscodeBackfill.BackfillRoutingKey,
            Arg.Is<byte[]>(body => IsParentTranscodeBackfill(body)),
            Arg.Any<CancellationToken>());

    [Fact]
    public async Task DeleteRuleAsync_last_rule_backfills_the_parent_policy_the_folder_falls_back_to()
    {
        GivenRuleToDelete(othersRemain: false);
        GivenParentFallback();

        await CreateSut().DeleteRuleAsync(RuleId, OwnerId, TestContext.Current.CancellationToken);

        await ParentBackfillEnqueued();
        Received.InOrder(() =>
        {
            _rules.Remove(Arg.Any<PolicyRule>());
            _unitOfWork.SaveChangesAsync(Arg.Any<CancellationToken>());
            _policies.ResolveInheritedPolicyAboveAsync(DirectoryId, Arg.Any<CancellationToken>());
            _unitOfWork.CommitAsync(Arg.Any<CancellationToken>());
        });
    }

    [Fact]
    public async Task DeleteRuleAsync_other_rules_remain_queues_nothing()
    {
        GivenRuleToDelete(othersRemain: true);
        GivenParentFallback();

        await CreateSut().DeleteRuleAsync(RuleId, OwnerId, TestContext.Current.CancellationToken);

        await NoBackfillEnqueued();
    }

    [Fact]
    public async Task DeleteRuleAsync_no_policy_above_queues_nothing()
    {
        GivenRuleToDelete(othersRemain: false);

        await CreateSut().DeleteRuleAsync(RuleId, OwnerId, TestContext.Current.CancellationToken);

        await NoBackfillEnqueued();
    }

    [Fact]
    public async Task DeletePolicyAsync_backfills_the_parent_policy_in_the_same_transaction()
    {
        _policies.GetByIdAsync(PolicyId, Arg.Any<CancellationToken>())
            .Returns(new DirectoryPolicy { Id = PolicyId, DirectoryId = DirectoryId });
        GivenParentFallback();

        await CreateSut().DeletePolicyAsync(PolicyId, OwnerId, TestContext.Current.CancellationToken);

        await ParentBackfillEnqueued();
        Received.InOrder(() =>
        {
            _unitOfWork.BeginTransactionAsync(Arg.Any<CancellationToken>());
            _policies.Remove(Arg.Any<DirectoryPolicy>());
            _policies.ResolveInheritedPolicyAboveAsync(DirectoryId, Arg.Any<CancellationToken>());
            _unitOfWork.CommitAsync(Arg.Any<CancellationToken>());
        });
    }

    private void GivenPolicyToUpdate(bool inherited)
    {
        _policies.GetByIdAsync(PolicyId, Arg.Any<CancellationToken>()).Returns(new DirectoryPolicy
        {
            Id = PolicyId,
            DirectoryId = DirectoryId,
            InheritedByChildren = inherited,
            CreatedAt = CreatedAt,
            Rules = [new PolicyRule { Id = RuleId, ActionType = PolicyActionType.AutoTag }],
        });
    }

    [Fact]
    public async Task UpdatePolicyAsync_stops_sharing_backfills_the_policy_its_subfolders_fall_back_to()
    {
        GivenPolicyToUpdate(inherited: true);
        GivenParentFallback();

        await CreateSut().UpdatePolicyAsync(PolicyId, false, OwnerId, TestContext.Current.CancellationToken);

        await BackfillEnqueued(UpdatedAt);
        await ParentBackfillEnqueued();
    }

    [Theory]
    [InlineData(false, true)]
    [InlineData(true, true)]
    [InlineData(false, false)]
    public async Task UpdatePolicyAsync_without_stopping_sharing_does_not_look_above(bool wasInherited, bool inherited)
    {
        GivenPolicyToUpdate(wasInherited);
        GivenParentFallback();

        await CreateSut().UpdatePolicyAsync(PolicyId, inherited, OwnerId, TestContext.Current.CancellationToken);

        await _policies.DidNotReceive()
            .ResolveInheritedPolicyAboveAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AddRuleAsync_saves_the_rule_and_its_backfill_in_one_transaction()
    {
        var result = await CreateSut().AddRuleAsync(PolicyId, RuleRequest(PolicyActionType.AutoTag), OwnerId,
            TestContext.Current.CancellationToken);

        result.Id.Should().Be(RuleId);
        Received.InOrder(() =>
        {
            _unitOfWork.BeginTransactionAsync(Arg.Any<CancellationToken>());
            _rules.AddAsync(Arg.Any<PolicyRule>(), Arg.Any<CancellationToken>());
            _outbox.EnqueueAsync(EnrichmentBackfill.BackfillRoutingKey, Arg.Any<byte[]>(),
                Arg.Any<CancellationToken>());
            _unitOfWork.CommitAsync(Arg.Any<CancellationToken>());
        });
    }
}