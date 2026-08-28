using Alexandria.Data.Models.Enumerators.Monitoring;
using Alexandria.Services.Monitoring;
using AwesomeAssertions;

namespace Alexandria.Tests.Unit.Monitoring;

public class JobOutcomeTrackerTests
{
    private sealed class MutableTimeProvider(DateTimeOffset start) : TimeProvider
    {
        private DateTimeOffset _now = start;

        public void AdvanceTo(DateTimeOffset value) => _now = value;

        public override DateTimeOffset GetUtcNow() => _now;
    }

    private readonly MutableTimeProvider _clock = new(new DateTimeOffset(2026, 8, 21, 12, 0, 0, TimeSpan.Zero));
    private readonly JobOutcomeTracker _sut;

    public JobOutcomeTrackerTests()
    {
        _sut = new JobOutcomeTracker(_clock);
    }

    [Fact]
    public void get_counts_since_unknown_service_returns_zeroes()
    {
        var counts = _sut.GetCountsSince(ServiceType.Lyrics, _clock.GetUtcNow().UtcDateTime.AddHours(-1));

        counts.Should().Be((0, 0));
    }

    [Fact]
    public void counts_successes_and_failures_separately()
    {
        _sut.RecordSuccess(ServiceType.Lyrics);
        _sut.RecordFailure(ServiceType.Lyrics);
        _sut.RecordFailure(ServiceType.Lyrics);

        var (failures, total) = _sut.GetCountsSince(ServiceType.Lyrics, _clock.GetUtcNow().UtcDateTime.AddMinutes(-5));

        failures.Should().Be(2);
        total.Should().Be(3);
    }

    [Fact]
    public void services_are_isolated_from_each_other()
    {
        _sut.RecordFailure(ServiceType.MediaPreviews);
        _sut.RecordFailure(ServiceType.MediaPreviews);
        _sut.RecordSuccess(ServiceType.Transpilation);

        _sut.GetCountsSince(ServiceType.Transpilation, _clock.GetUtcNow().UtcDateTime.AddMinutes(-1))
            .Should().Be((0, 1));
        _sut.GetCountsSince(ServiceType.DocumentPreviews, _clock.GetUtcNow().UtcDateTime.AddMinutes(-1))
            .Should().Be((0, 0));
    }

    [Fact]
    public void entries_older_than_cutoff_are_excluded()
    {
        _sut.RecordFailure(ServiceType.Lyrics);

        _clock.AdvanceTo(_clock.GetUtcNow().AddHours(3));

        var (failures, total) = _sut.GetCountsSince(ServiceType.Lyrics, _clock.GetUtcNow().UtcDateTime.AddHours(-2));

        failures.Should().Be(0);
        total.Should().Be(0);
    }

    [Fact]
    public void entry_exactly_at_cutoff_is_included()
    {
        _sut.RecordFailure(ServiceType.Lyrics);
        var cutoff = _clock.GetUtcNow().UtcDateTime;

        _clock.AdvanceTo(_clock.GetUtcNow().AddMinutes(10));

        var (failures, total) = _sut.GetCountsSince(ServiceType.Lyrics, cutoff);

        failures.Should().Be(1);
        total.Should().Be(1);
    }

    [Fact]
    public void pruning_is_permanent_across_queries()
    {
        _sut.RecordSuccess(ServiceType.Lyrics);

        _clock.AdvanceTo(_clock.GetUtcNow().AddMinutes(30));
        var cutoff = _clock.GetUtcNow().UtcDateTime.AddMinutes(-10);
        _sut.GetCountsSince(ServiceType.Lyrics, cutoff);

        // The stale entry was dequeued during the first query; a second query
        // with an even older cutoff must not resurrect it.
        var (failures, total) = _sut.GetCountsSince(ServiceType.Lyrics, cutoff.AddMinutes(-100));

        failures.Should().Be(0);
        total.Should().Be(0);
    }
}