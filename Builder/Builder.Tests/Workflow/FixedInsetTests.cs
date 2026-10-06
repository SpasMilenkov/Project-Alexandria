using AwesomeAssertions;
using Builder.UI.Shell;
using Spectre.Console;

namespace Builder.Tests.Workflow;

// D52 contract: FixedInset always starts content at the same column and caps
// the wrap width - the alignment guarantee behind every screen.
public class FixedInsetTests
{
    [Fact]
    public void Apply_usesInsetAsLeftPad_andCapsWrapWidthOnTheRight()
    {
        var content = new Markup("hello");
        const int availableWidth = 100;
        const int inset = 4;
        const int maxMeasure = 78;

        var (renderable, leftPad, rightPad) = Shell.FixedInset(content, availableWidth, inset, maxMeasure);

        leftPad.Should().Be(inset);
        rightPad.Should().Be(availableWidth - inset - maxMeasure);
        renderable.Should().NotBeNull();
    }

    [Fact]
    public void Apply_narrowTerminal_zeroesRightPad_insteadOfNegative()
    {
        var content = new Markup("hello");
        const int availableWidth = 40;
        const int inset = 4;
        const int maxMeasure = 78;

        var (_, leftPad, rightPad) = Shell.FixedInset(content, availableWidth, inset, maxMeasure);

        leftPad.Should().Be(inset);
        rightPad.Should().Be(0);
    }
}
