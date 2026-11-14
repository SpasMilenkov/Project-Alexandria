using AwesomeAssertions;
using Builder.Tests.Support;
using Builder.UI.Shell;
using Builder.UI.Shell.Screens;
using Xunit;

namespace Builder.Tests.UI;

[Collection(nameof(StaticConsoleTests))]
public class ShellWalkTests
{
    [Fact]
    public void RunAll_skippedScreen_passesThroughInTheDirectionOfTravel()
    {
        using var harness = new ShellHarness(new FakeKeys());
        var visits = new List<string>();
        var firstVisits = 0;
        var lastVisits = 0;

        harness.Shell.RunAll(
        [
            ("First", _ =>
            {
                visits.Add("First");
                firstVisits++;

                return ScreenOutcome.Continue;
            }),
            ("Skipped", _ =>
            {
                visits.Add("Skipped");

                return ScreenOutcome.Skip;
            }),
            ("Last", _ =>
            {
                visits.Add("Last");
                lastVisits++;

                return lastVisits == 1 ? ScreenOutcome.Back : ScreenOutcome.Continue;
            }),
        ]);

        visits.Should().Equal("First", "Skipped", "Last", "Skipped", "First", "Skipped", "Last");
        firstVisits.Should().Be(2);
        harness.Context.Completed.Should().BeTrue();
    }
}
