using System.Text.Json;
using Alexandria.Common;
using Alexandria.Common.Policies;
using Alexandria.Common.Repositories;
using Alexandria.Common.Services;
using Alexandria.Data.Models.Enumerators;
using Alexandria.Dto.Policies;
using Alexandria.Services.Storage.Policies;
using AwesomeAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace Alexandria.Tests.Unit.Policies;

public class PolicyDispatcherTests
{
    private static readonly Guid DirectoryId = Guid.NewGuid();
    private static readonly Guid FileId = Guid.NewGuid();

    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly IDirectoryPolicyRepository _policies = Substitute.For<IDirectoryPolicyRepository>();
    private readonly IJobQueue _jobQueue = Substitute.For<IJobQueue>();

    public PolicyDispatcherTests()
    {
        _unitOfWork.DirectoryPolicies.Returns(_policies);
    }

    private PolicyDispatcher CreateSut() =>
        new(_unitOfWork, _jobQueue, Substitute.For<ILogger<PolicyDispatcher>>());

    private void GivenRules(params PolicyRuleDto[] rules)
    {
        var policy = new DirectoryPolicyDto
        {
            Id = Guid.NewGuid(),
            DirectoryId = DirectoryId,
            Rules = rules,
        };

        _policies.ResolveEffectivePolicyAsync(DirectoryId, Arg.Any<CancellationToken>()).Returns(policy);
    }

    private static PolicyRuleDto Rule(PolicyActionType actionType, string parametersJson, int priority = 0) => new()
    {
        Id = Guid.NewGuid(),
        ActionType = actionType,
        TriggerType = PolicyTriggerType.AnyFile,
        TriggerValue = "*",
        Priority = priority,
        Parameters = JsonDocument.Parse(parametersJson).RootElement.Clone(),
    };

    private static FileFinalizedEvent AudioUpload() => new(
        FileId: FileId,
        VersionId: Guid.NewGuid(),
        DirectoryId: DirectoryId,
        MimeType: "audio/mpeg",
        FileName: "song.mp3",
        OwnerId: Guid.NewGuid(),
        IsNewVersion: false);

    [Fact]
    public async Task Backup_rule_is_skipped_and_later_rules_still_run()
    {
        GivenRules(
            Rule(PolicyActionType.Backup, """{ "destinationPath": "/backups/", "frequency": 0 }""", priority: 0),
            Rule(PolicyActionType.AutoTag, """{ "source": 1 }""", priority: 1));

        await CreateSut().DispatchAsync(AudioUpload());

        await _jobQueue.DidNotReceiveWithAnyArgs().QueueBackupAsync(default, null!, default);
        await _jobQueue.Received(1).QueueAutoTagAsync(FileId, Arg.Any<AutoTagParameters>(), "audio/mpeg",
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Backup_only_policy_queues_nothing()
    {
        GivenRules(Rule(PolicyActionType.Backup, """{ "destinationPath": "/backups/", "frequency": 2 }"""));

        await CreateSut().DispatchAsync(AudioUpload());

        _jobQueue.ReceivedCalls().Should().BeEmpty();
    }
}
