using Alexandria.Services.Storage.Notes;
using AwesomeAssertions;

namespace Alexandria.Tests.Unit.Notes;

public class NoteExcerptsTests
{
    private const string Id = "0b8c7a4e-36f1-4a6c-9a7e-1f0f3c2d4b5a";

    [Fact]
    public void From_headingTitle_isLeftOutAndMarkersRemoved()
    {
        const string text = "# Trip plan\n\n## Flights\n- Book by Friday\n> Hotel next";

        NoteExcerpts.From(text, "Trip plan.md").Should().Be("Flights Book by Friday Hotel next");
    }

    [Fact]
    public void From_firstLineMatchingTheName_isTheTitle()
    {
        NoteExcerpts.From("**Groceries**\nmilk, eggs", "Groceries.md").Should().Be("milk, eggs");
    }

    [Fact]
    public void From_firstLineThatIsNotTheTitle_isKept()
    {
        NoteExcerpts.From("milk, eggs\nbread", "Groceries.md").Should().Be("milk, eggs bread");
    }

    [Fact]
    public void From_frontmatterAndCode_areSkipped()
    {
        const string text = "---\ntags: [travel]\n---\n# Title\n```js\nconst x = 1;\n```\nAfter the code";

        NoteExcerpts.From(text, "Title.md").Should().Be("After the code");
    }

    [Fact]
    public void From_onlyATitle_hasNoExcerpt()
    {
        NoteExcerpts.From("# Lonely\n\n---\n", "Lonely.md").Should().BeNull();
    }

    [Fact]
    public void From_longText_isCutAtAWordWithAnEllipsis()
    {
        var text = "# T\n" + string.Join(' ', Enumerable.Repeat("word", 80));

        var excerpt = NoteExcerpts.From(text, "T.md");

        excerpt.Should().EndWith("word…");
        excerpt!.Length.Should().BeLessThanOrEqualTo(NoteExcerpts.MaxLength + 1);
    }

    [Fact]
    public void From_cutInsideALink_keepsTheLinkWhole()
    {
        var filler = string.Join(' ', Enumerable.Repeat("word", 42));
        var link = $"[the lecture recording](alexandria://file/{Id}?t=90)";
        var text = $"# T\n{filler} {link} and more text after it";

        var excerpt = NoteExcerpts.From(text, "T.md");

        excerpt.Should().Contain(link);
        excerpt.Should().EndWith("…");
    }
}
