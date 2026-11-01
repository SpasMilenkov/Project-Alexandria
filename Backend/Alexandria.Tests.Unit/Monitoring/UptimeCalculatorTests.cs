using Alexandria.Data.Models.Enumerators.Monitoring;
using Alexandria.Dto.Events.Operational;
using Alexandria.Services.Monitoring;
using AwesomeAssertions;

namespace Alexandria.Tests.Unit.Monitoring;

public class UptimeCalculatorTests
{
    private static readonly DateTime From = new(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime To = From.AddDays(1);
    private static readonly DateTime Later = To.AddDays(30);

    private static DowntimeInterval Down(double startHour, double? endHour) =>
        new(ServiceType.Api, From.AddHours(startHour), endHour is null ? null : From.AddHours(endHour.Value));

    [Fact]
    public void calculate_no_incidents_is_full_uptime()
    {
        UptimeCalculator.Calculate([], From, To, Later).Should().Be(100.0);
    }

    [Fact]
    public void calculate_overlapping_incidents_count_downtime_once()
    {
        var result = UptimeCalculator.Calculate([Down(0, 12), Down(0, 12), Down(6, 18)], From, To, Later);

        result.Should().BeApproximately(25.0, 0.0001);
    }

    [Fact]
    public void calculate_disjoint_incidents_sum()
    {
        var result = UptimeCalculator.Calculate([Down(0, 6), Down(12, 18)], From, To, Later);

        result.Should().BeApproximately(50.0, 0.0001);
    }

    [Fact]
    public void calculate_incident_started_before_window_is_clipped_to_from()
    {
        var result = UptimeCalculator.Calculate([Down(-48, 6)], From, To, Later);

        result.Should().BeApproximately(75.0, 0.0001);
    }

    [Fact]
    public void calculate_open_incident_is_clipped_to_now_and_future_is_excluded()
    {
        var now = From.AddHours(12);

        var result = UptimeCalculator.Calculate([Down(6, null)], From, To, now);

        result.Should().BeApproximately(50.0, 0.0001);
    }

    [Fact]
    public void calculate_window_entirely_in_future_is_full_uptime()
    {
        var result = UptimeCalculator.Calculate([Down(0, null)], From, To, From.AddHours(-1));

        result.Should().Be(100.0);
    }

    [Fact]
    public void calculate_never_goes_below_zero()
    {
        var result = UptimeCalculator.Calculate([Down(-5, null), Down(-1, 30)], From, To, Later);

        result.Should().Be(0.0);
    }
}
