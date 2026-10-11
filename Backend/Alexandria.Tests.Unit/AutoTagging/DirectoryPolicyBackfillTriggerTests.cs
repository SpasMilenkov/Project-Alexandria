using System.Linq.Expressions;
using System.Text.Json;
using Alexandria.Common;
using Alexandria.Common.Policies;
using Alexandria.Common.Repositories;
using Alexandria.Common.Services;
using Alexandria.Data.Models;
using Alexandria.Data.Models.Enumerators;
using Alexandria.Dto.Enrichment;
using Alexandria.Dto.Policies;
using Alexandria.Services.Storage.Policies;
using AwesomeAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
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
    private readonly IPublisherService _publisher = Substitute.For<IPublisherService>();

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

    private DirectoryPolicyService CreateSut(bool enabled = true)
    {
        var configuration = Substitute.For<IConfiguration>();
        configuration["Features:Autotagging"].Returns(enabled ? "true" : "false");

        return new DirectoryPolicyService(_unitOfWork, Substitute.For<IDirectoryService>(),
            _publisher, configuration, Substitute.For<ILogger<DirectoryPolicyService>>());
    }

    private static bool MatchesRequest(byte[] body, DateTime cutoff)
    {
        var request = JsonSerializer.Deserialize<EnrichmentBackfillRequest>(body, JsonOptions);

        return request == new EnrichmentBackfillRequest(PolicyId, RuleId, new DateTimeOffset(cutoff));
    }

    private static CreatePolicyRuleRequest RuleRequest(PolicyActionType actionType) => new()
    {
        ActionType = actionType,
        TriggerType = PolicyTriggerType.AnyFile,
        TriggerValue = "*",
        Parameters = JsonDocument.Parse("{}")
    };

    [Fact]
    public async Task CreatePolicyAsync_without_autotag_rule_does_not_publish_backfill()
    {
        await CreateSut().CreatePolicyAsync(new CreateDirectoryPolicyRequest { DirectoryId = DirectoryId },
            OwnerId, TestContext.Current.CancellationToken);

        await _publisher.DidNotReceive().PublishAsync(Arg.Any<byte[]>(), Arg.Any<string>());
    }

    [Theory]
    [InlineData(PolicyActionType.AutoTag)]
    [InlineData(PolicyActionType.Transcode)]
    public async Task UpdatePolicyAsync_inheritance_change_only_backfills_autotag_rules(PolicyActionType actionType)
    {
        _policies.GetByIdAsync(PolicyId, Arg.Any<CancellationToken>()).Returns(new DirectoryPolicy
        {
            Id = PolicyId,
            DirectoryId = DirectoryId,
            CreatedAt = CreatedAt,
            Rules = [new PolicyRule
            {
                Id = RuleId,
                ActionType = actionType,
                Parameters = JsonDocument.Parse("{}")
            }]
        });

        await CreateSut().UpdatePolicyAsync(PolicyId, true, OwnerId, TestContext.Current.CancellationToken);

        if (actionType == PolicyActionType.AutoTag)
        {
            await _publisher.Received(1).PublishAsync(
                Arg.Is<byte[]>(body => MatchesRequest(body, UpdatedAt)), EnrichmentBackfill.BackfillRoutingKey);
        }
        else
        {
            await _publisher.DidNotReceive().PublishAsync(Arg.Any<byte[]>(), Arg.Any<string>());
        }
    }

    [Theory]
    [InlineData(PolicyActionType.AutoTag)]
    [InlineData(PolicyActionType.Transcode)]
    public async Task AddRuleAsync_only_autotag_publishes_rule_and_creation_cutoff(PolicyActionType actionType)
    {
        await CreateSut().AddRuleAsync(PolicyId, RuleRequest(actionType), OwnerId,
            TestContext.Current.CancellationToken);

        if (actionType == PolicyActionType.AutoTag)
        {
            await _publisher.Received(1).PublishAsync(
                Arg.Is<byte[]>(body => MatchesRequest(body, CreatedAt)), EnrichmentBackfill.BackfillRoutingKey);
        }
        else
        {
            await _publisher.DidNotReceive().PublishAsync(Arg.Any<byte[]>(), Arg.Any<string>());
        }
    }

    [Theory]
    [InlineData(PolicyActionType.AutoTag)]
    [InlineData(PolicyActionType.Backup)]
    public async Task UpdateRuleAsync_only_autotag_publishes_rule_and_update_cutoff(PolicyActionType actionType)
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

        if (actionType == PolicyActionType.AutoTag)
        {
            await _publisher.Received(1).PublishAsync(
                Arg.Is<byte[]>(body => MatchesRequest(body, UpdatedAt)), EnrichmentBackfill.BackfillRoutingKey);
        }
        else
        {
            await _publisher.DidNotReceive().PublishAsync(Arg.Any<byte[]>(), Arg.Any<string>());
        }
    }

    [Fact]
    public async Task AddRuleAsync_autotagging_disabled_does_not_publish()
    {
        await CreateSut(enabled: false).AddRuleAsync(PolicyId, RuleRequest(PolicyActionType.AutoTag), OwnerId,
            TestContext.Current.CancellationToken);

        await _publisher.DidNotReceive().PublishAsync(Arg.Any<byte[]>(), Arg.Any<string>());
    }

    [Fact]
    public async Task AddRuleAsync_broker_failure_still_returns_saved_rule()
    {
        _publisher.PublishAsync(Arg.Any<byte[]>(), Arg.Any<string>())
            .Returns(Task.FromException(new InvalidOperationException("broker down")));

        var result = await CreateSut().AddRuleAsync(PolicyId, RuleRequest(PolicyActionType.AutoTag), OwnerId,
            TestContext.Current.CancellationToken);

        result.Id.Should().Be(RuleId);
        await _publisher.Received(1).PublishAsync(Arg.Any<byte[]>(), EnrichmentBackfill.BackfillRoutingKey);
    }
}
