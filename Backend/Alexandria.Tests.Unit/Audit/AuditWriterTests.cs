using System.Text.Json;
using Alexandria.Common.Audit;
using Alexandria.Common.Config;
using Alexandria.Data.Audit;
using Alexandria.Data.Context;
using Alexandria.Data.Models;
using Alexandria.Data.Models.Enumerators;
using Alexandria.Dto.Audit;
using AwesomeAssertions;
using Microsoft.EntityFrameworkCore;
using NSubstitute;

namespace Alexandria.Tests.Unit.Audit;

public class AuditWriterTests
{
    private readonly IAuditActorAccessor _actor = Substitute.For<IAuditActorAccessor>();
    private readonly Guid _correlationId = Guid.NewGuid();

    public AuditWriterTests()
    {
        _actor.CorrelationId.Returns(_correlationId);
        _actor.IpAddress.Returns("10.0.0.7");
    }

    // Never connects: Record only stages the entity in the change tracker.
    private static AlexandriaDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AlexandriaDbContext>()
            .UseNpgsql("Host=localhost;Database=alexandria_unit_tests;Username=postgres;Password=postgres")
            .Options;

        return new AlexandriaDbContext(options);
    }

    private static AuditEntry Entry(Guid? actorId = null) => new()
    {
        EventCode = AuditEventCode.FileDownloaded,
        OperationType = OperationType.Read,
        EntityType = EntityType.File,
        EntityId = Guid.NewGuid(),
        OwnerId = Guid.NewGuid(),
        EntityName = "song.flac",
        ActorId = actorId,
        Metadata = new Dictionary<string, object?> { ["mime_type"] = "audio/flac", ["new_ip"] = true },
    };

    [Fact]
    public void Record_stagesWithoutSaving()
    {
        using var context = CreateContext();
        var userId = Guid.NewGuid();
        _actor.UserId.Returns(userId);

        new AuditWriter(context, _actor).Record(Entry());

        var staged = context.ChangeTracker.Entries<AuditLog>().Single();
        staged.State.Should().Be(EntityState.Added);
        staged.Entity.UserId.Should().Be(userId);
        staged.Entity.Source.Should().Be(LogSource.API);
        staged.Entity.CorrelationId.Should().Be(_correlationId);
        staged.Entity.IpAddress.Should().Be("10.0.0.7");
    }

    [Fact]
    public void Record_noActor_fallsBackToSystem()
    {
        using var context = CreateContext();
        _actor.UserId.Returns((Guid?)null);

        new AuditWriter(context, _actor).Record(Entry());

        var log = context.ChangeTracker.Entries<AuditLog>().Single().Entity;
        log.UserId.Should().Be(SystemConfig.SystemId);
        log.Source.Should().Be(LogSource.System);
    }

    [Fact]
    public void Record_explicitActor_overridesRequestActor()
    {
        using var context = CreateContext();
        var target = Guid.NewGuid();
        _actor.UserId.Returns((Guid?)null);

        new AuditWriter(context, _actor).Record(Entry(actorId: target));

        var log = context.ChangeTracker.Entries<AuditLog>().Single().Entity;
        log.UserId.Should().Be(target);
        log.Source.Should().Be(LogSource.API);
    }

    [Fact]
    public void Record_metadata_keepsSnakeCaseKeys()
    {
        using var context = CreateContext();

        new AuditWriter(context, _actor).Record(Entry());

        var json = context.ChangeTracker.Entries<AuditLog>().Single().Entity.MetadataJson!;
        using var document = JsonDocument.Parse(json);

        document.RootElement.GetProperty("mime_type").GetString().Should().Be("audio/flac");
        document.RootElement.GetProperty("new_ip").GetBoolean().Should().BeTrue();
    }

    [Fact]
    public void Record_longName_isTruncated()
    {
        using var context = CreateContext();
        var entry = new AuditEntry
        {
            EventCode = AuditEventCode.FileDownloaded,
            OperationType = OperationType.Read,
            EntityType = EntityType.File,
            EntityName = new string('n', 1500),
        };

        new AuditWriter(context, _actor).Record(entry);

        context.ChangeTracker.Entries<AuditLog>().Single().Entity.EntityName.Should().HaveLength(1000);
    }
}
