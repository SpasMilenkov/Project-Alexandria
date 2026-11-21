using Alexandria.Services.Storage.Notes;
using AwesomeAssertions;

namespace Alexandria.Tests.Unit.Notes;

public class NoteTitlesTests
{
    [Theory]
    [InlineData("# Trip plans\n\nBook the train.", "Trip plans")]
    [InlineData("Trip plans\n\nBook the train.", "Trip plans")]
    [InlineData("\n\n## Trip plans ##\nmore", "Trip plans")]
    [InlineData("- [ ] Pack the **bags**\n- [ ] Book", "Pack the bags")]
    [InlineData("> Quoted *idea*\nmore", "Quoted idea")]
    [InlineData("1. First step\n2. Second", "First step")]
    [InlineData("See [the map](https://example.com) here\nmore", "See the map here")]
    [InlineData("<alexandria://file/123> notes\nmore", "notes")]
    [InlineData("Read snake_case and `code`\nmore", "Read snake_case and code")]
    public void FromMarkdown_finishedFirstLine_becomesThePlainTitle(string markdown, string expected)
    {
        NoteTitles.FromMarkdown(markdown).Should().Be(expected);
    }

    [Fact]
    public void FromMarkdown_frontmatterRulesAndCode_areSkipped()
    {
        const string markdown = "---\ntags: [travel]\n---\n\n***\n\n```js\nconst x = 1;\n```\n\nPacking list\nsocks";

        NoteTitles.FromMarkdown(markdown).Should().Be("Packing list");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   \n\n")]
    [InlineData("# Trip pl")]
    [InlineData("# Trip plans\n\n")]
    [InlineData("---\n\n***\n")]
    public void FromMarkdown_noFinishedLineYet_givesNothing(string? markdown)
    {
        NoteTitles.FromMarkdown(markdown).Should().BeNull();
    }

    [Fact]
    public void FromMarkdown_longLine_isCutAtAWord()
    {
        var line = string.Join(' ', Enumerable.Repeat("word", 40));

        var title = NoteTitles.FromMarkdown(line + "\nmore");

        title.Should().NotBeNull();
        title!.Length.Should().BeLessThanOrEqualTo(NoteTitles.MaxLength);
        title.Should().EndWith("word");
    }
}
