using System.Text;
using Alexandria.Common;
using Alexandria.Common.Policies;
using Alexandria.Common.Repositories;
using Alexandria.Common.Services;
using Alexandria.Data.Models;
using Alexandria.Dto.Policies;
using Alexandria.Services.Storage.Policies;
using AwesomeAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace Alexandria.Tests.Unit.AutoTagging;

public class DirectoryPolicyBackfillTriggerTests
{
    private static readonly Guid DirectoryId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid PolicyId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid OwnerId = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc");

    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly IDirectoryPolicyRepository _policies = Substitute.For<IDirectoryPolicyRepository>();
    private readonly IPublisherService _publisher = Substitute.For<IPublisherService>();

    public DirectoryPolicyBackfillTriggerTests()
    {
        _unitOfWork.DirectoryPolicies.Returns(_policies);
    }

    private DirectoryPolicyService CreateSut() => new(
        _unitOfWork,
        Substitute.For<IDirectoryService>(),
        _publisher,
        Substitute.For<ILogger<DirectoryPolicyService>>());

    [Fact]
    public async Task CreatePolicyAsync_publishes_backfill_for_new_policy()
    {
        _policies.ExistsForDirectoryAsync(DirectoryId, Arg.Any<CancellationToken>()).Returns(false);
        _policies.AddAsync(Arg.Any<DirectoryPolicy>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                var entity = call.Arg<DirectoryPolicy>();
                entity.Id = PolicyId;
                return entity;
            });

        var result = await CreateSut().CreatePolicyAsync(
            new CreateDirectoryPolicyRequest { DirectoryId = DirectoryId },
            OwnerId,
            TestContext.Current.CancellationToken);

        result.Id.Should().Be(PolicyId);
        await _publisher.Received(1).PublishAsync(
            Arg.Is<byte[]>(body => Encoding.UTF8.GetString(body) == PolicyId.ToString()),
            EnrichmentBackfill.BackfillRoutingKey);
    }

    [Fact]
    public async Task UpdatePolicyAsync_publishes_backfill()
    {
        _policies.GetByIdAsync(PolicyId, Arg.Any<CancellationToken>())
            .Returns(new DirectoryPolicy { Id = PolicyId, DirectoryId = DirectoryId });

        await CreateSut().UpdatePolicyAsync(
            PolicyId, inheritedByChildren: true, OwnerId,
            TestContext.Current.CancellationToken);

        await _publisher.Received(1).PublishAsync(
            Arg.Is<byte[]>(body => Encoding.UTF8.GetString(body) == PolicyId.ToString()),
            EnrichmentBackfill.BackfillRoutingKey);
    }

    [Fact]
    public async Task CreatePolicyAsync_broker_failure_still_returns_policy()
    {
        _policies.ExistsForDirectoryAsync(DirectoryId, Arg.Any<CancellationToken>()).Returns(false);
        _policies.AddAsync(Arg.Any<DirectoryPolicy>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                var entity = call.Arg<DirectoryPolicy>();
                entity.Id = PolicyId;
                return entity;
            });
        _publisher.PublishAsync(Arg.Any<byte[]>(), Arg.Any<string>())
            .Returns(Task.FromException(new InvalidOperationException("broker down")));

        var result = await CreateSut().CreatePolicyAsync(
            new CreateDirectoryPolicyRequest { DirectoryId = DirectoryId },
            OwnerId,
            TestContext.Current.CancellationToken);

        result.Id.Should().Be(PolicyId);
    }
}