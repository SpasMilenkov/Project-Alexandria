using AwesomeAssertions;
using Builder.UI.Shell.Screens;
using Builder.UI.Shell.Screens.Components;
using Builder.UI.Shell.Splash;
using Spectre.Console.Rendering;
using Spectre.Console.Testing;

namespace Builder.Tests.UI;

public class CitySceneTests
{
    [Theory]
    [InlineData(1, 1, 1, 1, '┼')]
    [InlineData(0, 0, 2, 2, '━')]
    [InlineData(2, 2, 0, 2, '┣')]
    [InlineData(1, 0, 2, 2, '┷')]
    [InlineData(0, 0, 2, 0, '╸')]
    public void BoxGlyphs_armWeights_pickTheMatchingJunction(int up, int down, int left, int right, char expected)
    {
        BoxGlyphs.For(up, down, left, right).Should().Be(expected);
    }

    [Fact]
    public void Splash_settled_showsCartoucheCityAndMapKey()
    {
        var lines = Render(new CityScene { Height = CityPlan.MinSplashHeight, Splash = true, Elapsed = CityScene.Duration }, 80);
        var text = string.Join("\n", lines);

        lines.Should().HaveCount(CityPlan.MinSplashHeight);
        text.Should().Contain(Views.Wordmark[0]).And.Contain(Views.Wordmark[1]);
        text.Should().Contain(Views.Tagline).And.Contain(Views.InstallerVersion);
        text.Should().Contain("CANOPIC WAY").And.Contain("╋").And.Contain("▲");
        text.Should().Contain("EUNOSTOS").And.Contain("L A K E   M A R E O T I S").And.Contain("1 km");
    }

    [Fact]
    public void Splash_wideTerminal_keepsMoreOfThePlansStreets()
    {
        static int StreetRows(List<string> lines) => lines.Count(line => line.Contains('┼') || line.Contains('╋'));

        var small = Render(new CityScene { Height = CityPlan.MinSplashHeight, Splash = true }, 80);
        var large = Render(new CityScene { Height = 41, Splash = true }, 140);

        StreetRows(large).Should().BeGreaterThan(StreetRows(small));
        StreetRows(large).Should().BeLessThanOrEqualTo(CityMapData.LongStreets.Length);
    }

    [Fact]
    public void Splash_atStart_hasNothingLaidOutYet()
    {
        var text = string.Join("\n", Render(new CityScene { Height = 30, Splash = true, Elapsed = 0 }, 120));

        text.Should().NotContain("━").And.NotContain("─").And.NotContain("▀").And.NotContain("▲");
    }

    [Fact]
    public void Splash_terminalTooSmall_rendersBlankRowsOfTheRequestedSize()
    {
        var lines = Render(new CityScene { Height = 12, Splash = true }, 60);

        lines.Should().HaveCount(12);
        lines.Should().OnlyContain(line => string.IsNullOrWhiteSpace(line));
    }

    [Fact]
    public void FinishedStrip_isTheCityAroundTheCanopicWay()
    {
        var lines = Render(new CityScene { Height = 7, Complete = true }, 78);

        lines.Should().HaveCount(7);
        lines.Should().Contain(line => line.Contains("━━━━") && line.Contains('╋'));
        string.Join("\n", lines).Should().Contain("▓");
    }

    private static List<string> Render(IRenderable scene, int width)
    {
        var console = new TestConsole().Width(width);

        console.Profile.Capabilities.Unicode = true;
        console.Write(scene);

        return console.Output.TrimEnd('\n').Split('\n').ToList();
    }
}
