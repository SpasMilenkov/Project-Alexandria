using Alexandria.Dto.Files;
using Alexandria.Repositories.Search;
using AwesomeAssertions;

namespace Alexandria.Tests.Unit.Repositories;

public class ContentMatchParserTests
{
    private const char Fragment = ContentMatchParser.FragmentDelimiter;
    private const char Start = ContentMatchParser.MatchStart;
    private const char End = ContentMatchParser.MatchEnd;

    [Fact]
    public void Parse_splitsMatchesFromTheirContext()
    {
        var fragments = ContentMatchParser.Parse($"Hotel {Start}invoice{End} from {Start}Paris{End}, total due");

        fragments.Should().ContainSingle().Which.Segments.Should().Equal(
            new ContentMatchSegment("Hotel ", false),
            new ContentMatchSegment("invoice", true),
            new ContentMatchSegment(" from ", false),
            new ContentMatchSegment("Paris", true),
            new ContentMatchSegment(", total due", false));
    }

    [Fact]
    public void Parse_keepsEachExcerptApart_inOrder()
    {
        var fragments = ContentMatchParser.Parse(
            $"first {Start}paris{End} mention{Fragment}second {Start}paris{End} mention");

        fragments.Should().HaveCount(2);
        fragments[0].Segments[0].Text.Should().Be("first ");
        fragments[1].Segments[0].Text.Should().Be("second ");
    }

    [Fact]
    public void Parse_dropsExcerptsWithoutAMatch()
    {
        var fragments = ContentMatchParser.Parse($"opening words only{Fragment}a {Start}match{End}");

        fragments.Should().ContainSingle().Which.Segments.Should().Contain(s => s.IsMatch && s.Text == "match");
    }

    [Fact]
    public void Parse_turnsLineAndPageBreaksIntoSpaces_andTrimsTheEdges()
    {
        var fragments = ContentMatchParser.Parse($"  end of page\f{Start}Budget{End}\nnext line  ");

        fragments.Should().ContainSingle().Which.Segments.Should().Equal(
            new ContentMatchSegment("end of page ", false),
            new ContentMatchSegment("Budget", true),
            new ContentMatchSegment(" next line", false));
    }

    [Fact]
    public void Parse_matchAtBothEdges_keepsOnlyMatchSegments()
    {
        var fragments = ContentMatchParser.Parse($"{Start}invoice{End} {Start}paris{End}");

        fragments.Should().ContainSingle().Which.Segments.Should().Equal(
            new ContentMatchSegment("invoice", true),
            new ContentMatchSegment(" ", false),
            new ContentMatchSegment("paris", true));
    }

    [Fact]
    public void Parse_empty_returnsNothing()
    {
        ContentMatchParser.Parse("").Should().BeEmpty();
    }
}