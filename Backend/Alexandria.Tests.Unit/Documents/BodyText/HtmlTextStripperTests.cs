using Alexandria.Services.Preview.Documents.BodyText;
using AwesomeAssertions;

namespace Alexandria.Tests.Unit.Documents.BodyText;

public class HtmlTextStripperTests
{
    [Theory]
    [InlineData("<p>One</p><p>Two</p>", "One\nTwo")]
    [InlineData("<p>A <em>bold</em> claim</p>", "A bold claim")]
    [InlineData("<p>split<span>word</span></p>", "splitword")]
    [InlineData("line<br/>break", "line\nbreak")]
    [InlineData("<table><tr><td>Hotel</td><td>412</td></tr><tr><td>Taxi</td></tr></table>", "Hotel 412\nTaxi")]
    [InlineData("Caf&eacute;&nbsp;au&#160;lait &amp; cr&#xE8;me", "Café au lait & crème")]
    [InlineData("<html:p>namespaced</html:p><html:p>tags</html:p>", "namespaced\ntags")]
    public void Strip_keepsTextAndBlockStructure(string html, string expected)
    {
        Strip(html).Should().Be(expected);
    }

    [Fact]
    public void Strip_dropsHeadScriptsStylesAndComments()
    {
        const string html = """
                            <!DOCTYPE html><html><head><title>Repeated</title></head>
                            <body><!-- hidden note --><script>var x = "<p>no</p>";</script><style>p{}</style><p>Visible</p></body></html>
                            """;

        Strip(html).Should().Be("Visible");
    }

    [Fact]
    public void Strip_ignoresAngleBracketsInsideAttributes()
    {
        Strip("""<a title="a > b">link</a> text""").Should().Be("link text");
    }

    [Fact]
    public void Strip_keepsCdataText()
    {
        Strip("<p><![CDATA[raw <text>]]></p>").Should().Be("raw <text>");
    }

    [Fact]
    public void Strip_unterminatedMarkup_endsQuietly()
    {
        Strip("<p>kept</p><p class=\"broken").Should().Be("kept");
    }

    private static string Strip(string html)
    {
        var collector = new TextCollector();

        HtmlTextStripper.Strip(html, collector);

        return collector.Build();
    }
}