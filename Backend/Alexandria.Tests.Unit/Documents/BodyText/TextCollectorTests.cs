using Alexandria.Services.Preview.Documents.BodyText;
using AwesomeAssertions;

namespace Alexandria.Tests.Unit.Documents.BodyText;

public class TextCollectorTests
{
    [Fact]
    public void Append_collapsesWhitespaceRuns()
    {
        var collector = new TextCollector();

        collector.Append("  Hotel \t  invoice  \r\n\r\n\n  Paris  ");

        collector.Build().Should().Be("Hotel invoice\nParis");
    }

    [Fact]
    public void Append_dropsControlCharactersAndReplacementMarks()
    {
        var collector = new TextCollector();

        collector.Append("in\0voi\u0007ce�");

        collector.Build().Should().Be("invoice");
        collector.VisibleCharacters.Should().Be(7);
    }

    [Fact]
    public void Append_keepsSurrogatePairs_andDropsBrokenHalves()
    {
        var collector = new TextCollector();

        collector.Append("a😀b\uD83Dc\uDE00d");

        collector.Build().Should().Be("a😀bcd");
    }

    [Fact]
    public void PageBreak_keepsEveryPage_evenEmptyOnes()
    {
        var collector = new TextCollector();

        collector.Append("one \n");
        collector.PageBreak();
        collector.PageBreak();
        collector.Append("three");

        collector.Build().Should().Be("one\f\fthree");
    }

    [Fact]
    public void Separators_neverLeadOrTrail()
    {
        var collector = new TextCollector();

        collector.LineBreak();
        collector.Space();
        collector.Append("word");
        collector.LineBreak();
        collector.PageBreak();

        collector.Build().Should().Be("word");
    }

    [Fact]
    public void LineBreak_winsOverSpace_betweenTwoWords()
    {
        var collector = new TextCollector();

        collector.Append("first");
        collector.Space();
        collector.LineBreak();
        collector.Space();
        collector.Append("second");

        collector.Build().Should().Be("first\nsecond");
    }

    [Fact]
    public void Append_stopsAtTheLimit_andMarksTheTextTruncated()
    {
        var collector = new TextCollector(maxCharacters: 10);

        collector.Append("invoice paris hotel");

        collector.IsFull.Should().BeTrue();
        collector.IsTruncated.Should().BeTrue();
        collector.Build().Should().Be("invoice pa");
    }

    [Fact]
    public void Append_inline_treatsSourceNewlinesAsSpaces()
    {
        var collector = new TextCollector();

        collector.Append("Hotel\n   invoice\r\n\fParis", inline: true);

        collector.Build().Should().Be("Hotel invoice Paris");
    }

    [Fact]
    public void Append_neverSplitsASurrogatePairAtTheLimit()
    {
        var collector = new TextCollector(maxCharacters: 2);

        collector.Append("a😀");

        collector.Build().Should().Be("a");
    }

    [Fact]
    public void MarkTruncated_keepsTheTextButFlagsIt()
    {
        var collector = new TextCollector();

        collector.Append("partial");
        collector.MarkTruncated();

        collector.IsFull.Should().BeFalse();
        collector.IsTruncated.Should().BeTrue();
        collector.Build().Should().Be("partial");
    }
}