using Alexandria.Data.Context;
using Alexandria.Data.Models;
using Alexandria.Data.Models.Enumerators.Monitoring;
using Alexandria.Repositories;
using AwesomeAssertions;
using Microsoft.EntityFrameworkCore;

namespace Alexandria.Tests.Unit.Repositories;

public class OperationalEventQueryTests
{
    private static readonly Guid Owner = Guid.NewGuid();
    private static readonly DateTime From = new(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc);

    private static AlexandriaDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AlexandriaDbContext>()
            .UseNpgsql("Host=localhost;Database=translation_only;Username=postgres;Password=postgres")
            .Options;

        return new AlexandriaDbContext(options);
    }

    private static OperationalEvent Event(OperationalEventSeverity severity, ServiceType service, DateTime created)
    {
        return new OperationalEvent
        {
            Id = Guid.NewGuid(), CreatedBy = Owner, Severity = severity, ServiceType = service,
            CreatedAt = created, Status = OperationalEventStatus.Active
        };
    }

    [Fact]
    public void CurrentStatusPerServiceQuery_failure_dominates_newer_partial_and_degraded_incidents()
    {
        var failure = Event(OperationalEventSeverity.Failure, ServiceType.Api, From);
        var partial = Event(OperationalEventSeverity.PartialFailure, ServiceType.Api, From.AddHours(1));
        var degraded = Event(OperationalEventSeverity.DegradedPerformance, ServiceType.Api, From.AddHours(2));
        var resolved = Event(OperationalEventSeverity.Failure, ServiceType.Lyrics, From.AddHours(2));
        resolved.Status = OperationalEventStatus.Resolved;
        var lyricsPartial = Event(OperationalEventSeverity.PartialFailure, ServiceType.Lyrics, From);
        var lyricsDegraded = Event(OperationalEventSeverity.DegradedPerformance, ServiceType.Lyrics, From.AddHours(2));

        var result = OperationalEventRepository.CurrentStatusPerServiceQuery(
            new[] { degraded, partial, failure, resolved, lyricsPartial, lyricsDegraded }.AsQueryable()).ToList();

        result.Should().BeEquivalentTo(new[] { failure, lyricsPartial });
    }

    [Fact]
    public void CurrentStatusPerServiceQuery_npgsql_orders_explicit_severity_comparisons_instead_of_strings()
    {
        using var context = CreateContext();

        var sql = OperationalEventRepository.CurrentStatusPerServiceQuery(
            context.OperationalEvents.AsNoTracking()).ToQueryString();

        sql.Should().Contain("Severity\" = 'Failure'").And.Contain("Severity\" = 'PartialFailure'")
            .And.Contain("Status\" = 'Active'");
        sql.Should().NotContain("\"Severity\" DESC");
    }
}
