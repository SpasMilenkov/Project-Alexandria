using Alexandria.Data.Models.Enumerators;
using Alexandria.Services.Storage.Links;
using AwesomeAssertions;

namespace Alexandria.Tests.Unit.Notes;

public class NoteLinkExtractorTests
{
    private const string Trip = "0b8c7a4e-36f1-4a6c-9a7e-1f0f3c2d4b5a";
    private const string Paris = "5d2f9e10-8a3b-4c7d-b1e6-2a9f0c8d7e64";
    private const string Vacation = "9a1b2c3d-4e5f-4a6b-8c7d-0e1f2a3b4c5d";

    [Fact]
    public void Extract_everyLinkForm_findsEachTargetWithItsKind()
    {
        var markdown = $"""
            See <alexandria://file/{Trip}> and [the folder](alexandria://folder/{Paris}).

            ![cover](alexandria://tag/{Vacation})
            """;

        var links = NoteLinkExtractor.Extract(markdown);

        links.Select(l => (l.Kind, l.TargetId)).Should().Equal(
            (LinkTargetKind.File, Guid.Parse(Trip)),
            (LinkTargetKind.Folder, Guid.Parse(Paris)),
            (LinkTargetKind.Tag, Guid.Parse(Vacation)));
    }

    [Fact]
    public void Extract_sameTargetTwice_listsItOnceWithTheFirstLine()
    {
        var markdown = $"""
            First mention <alexandria://file/{Trip}>
            Second mention <alexandria://file/{Trip.ToUpperInvariant()}?t=90>
            """;

        var link = NoteLinkExtractor.Extract(markdown).Should().ContainSingle().Subject;

        link.Snippet.Should().Be($"First mention <alexandria://file/{Trip}>");
    }

    [Fact]
    public void Extract_linksInCode_areSkipped()
    {
        var markdown = $"""
            Inline `<alexandria://file/{Trip}>` example.

            ```md
            <alexandria://folder/{Paris}>
            ```

            Real <alexandria://tag/{Vacation}>
            """;

        var link = NoteLinkExtractor.Extract(markdown).Should().ContainSingle().Subject;

        link.Kind.Should().Be(LinkTargetKind.Tag);
        link.Snippet.Should().Be($"Real <alexandria://tag/{Vacation}>");
    }

    [Fact]
    public void Extract_unclosedFence_skipsEverythingAfterIt()
    {
        var markdown = $"""
            ~~~
            <alexandria://file/{Trip}>
            """;

        NoteLinkExtractor.Extract(markdown).Should().BeEmpty();
    }

    [Theory]
    [InlineData("# Heading with ")]
    [InlineData("- ")]
    [InlineData("> > ")]
    [InlineData("1. [x] ")]
    public void Extract_blockMarkers_areLeftOutOfTheSnippet(string prefix)
    {
        var markdown = $"{prefix}<alexandria://file/{Trip}> here";

        var link = NoteLinkExtractor.Extract(markdown).Should().ContainSingle().Subject;

        link.Snippet.Should().EndWith($"<alexandria://file/{Trip}> here");
        link.Snippet.Should().NotStartWith(prefix[..1]);
    }

    [Fact]
    public void Extract_longLine_cutsAroundTheLinkWithoutSplittingIt()
    {
        var before = string.Join(' ', Enumerable.Repeat("before", 60));
        var after = string.Join(' ', Enumerable.Repeat("after", 60));
        var link = $"[a label that matters](alexandria://file/{Trip})";
        var markdown = $"{before} {link} {after}";

        var snippet = NoteLinkExtractor.Extract(markdown).Should().ContainSingle().Subject.Snippet;

        snippet.Should().Contain(link);
        snippet.Should().StartWith("…before");
        snippet.Should().EndWith("after…");
        snippet.Length.Should().BeLessThanOrEqualTo(NoteLinkExtractor.SnippetLength + 2);
    }

    [Fact]
    public void Extract_windowEdgeInsideAnotherLink_keepsThatLinkWhole()
    {
        // The neighbour is 69 characters and the link starts at 130, so the window's natural start
        // (100 characters before the link) falls inside the neighbour.
        var neighbour = $"[neighbour](alexandria://folder/{Paris})";
        var filler = string.Join(' ', Enumerable.Repeat("word", 12));
        var after = string.Join(' ', Enumerable.Repeat("after", 60));
        var markdown = $"{neighbour} {filler} <alexandria://file/{Trip}> {after}";

        var trip = NoteLinkExtractor.Extract(markdown).Single(l => l.Kind == LinkTargetKind.File);

        trip.Snippet.Should().StartWith(neighbour);
        trip.Snippet.Should().Contain($"<alexandria://file/{Trip}>");
    }

    [Fact]
    public void Extract_linkGluedInsideALongWord_keepsTheLinkInItsSnippet()
    {
        // The window starts inside the word, and the word's next space comes after the link and
        // past the window's end, so snapping to it once cut the window to a negative length.
        var link = $"alexandria://file/{Trip}";
        var markdown = $"{new string('a', 300)}{link}{new string('b', 200)} end";

        var snippet = NoteLinkExtractor.Extract(markdown).Should().ContainSingle().Subject.Snippet;

        // The window stretches to keep the glued link whole, so only the hard limit applies.
        snippet.Should().Contain(link);
        snippet.Length.Should().BeLessThanOrEqualTo(900);
    }

    [Fact]
    public void Extract_textPostgresCannotStore_isLeftOutOfTheSnippet()
    {
        // A NUL and half an emoji go; a whole emoji stays.
        var markdown = $"Nul\0here, half \ud83d <alexandria://file/{Trip}> whole 😀";

        var snippet = NoteLinkExtractor.Extract(markdown).Should().ContainSingle().Subject.Snippet;

        snippet.Should().Be($"Nulhere, half  <alexandria://file/{Trip}> whole 😀");
    }

    [Fact]
    public void Extract_windowCutThroughAnEmoji_leavesNoHalfOfItBehind()
    {
        // The link starts at 121, so the window starts at 21: the second half of the eleventh
        // emoji. No space comes before the link, so nothing snaps the start to a word.
        var emoji = "😀";
        var before = string.Concat(Enumerable.Repeat(emoji, 60)) + "x";
        var after = string.Join(' ', Enumerable.Repeat("after", 60));
        var markdown = $"{before}<alexandria://file/{Trip}> {after}";

        var snippet = NoteLinkExtractor.Extract(markdown).Should().ContainSingle().Subject.Snippet;

        snippet.Should().StartWith($"…{emoji}");
        snippet.Should().Contain($"<alexandria://file/{Trip}>");
        HasUnpairedSurrogate(snippet).Should().BeFalse();
    }

    private static bool HasUnpairedSurrogate(string text)
    {
        var i = 0;

        while (i < text.Length)
        {
            if (char.IsHighSurrogate(text[i]) && i + 1 < text.Length && char.IsLowSurrogate(text[i + 1]))
            {
                i += 2;
                continue;
            }

            if (char.IsSurrogate(text[i])) return true;

            i++;
        }

        return false;
    }

    [Fact]
    public void Extract_noLinks_returnsNothing()
    {
        NoteLinkExtractor.Extract("Just text, and https://example.com.").Should().BeEmpty();
    }

    [Fact]
    public void Extract_crlfLineEndings_keepSnippetsToTheirLine()
    {
        var markdown = $"one\r\ntwo <alexandria://file/{Trip}>\r\nthree";

        var link = NoteLinkExtractor.Extract(markdown).Should().ContainSingle().Subject;

        link.Snippet.Should().Be($"two <alexandria://file/{Trip}>");
    }

    [Fact]
    public void Extract_moreTargetsThanTheLimit_stopsAtTheLimit()
    {
        var markdown = string.Join('\n', Enumerable.Range(0, NoteLinkExtractor.MaxTargets + 5)
            .Select(_ => $"<alexandria://file/{Guid.NewGuid()}>"));

        NoteLinkExtractor.Extract(markdown).Should().HaveCount(NoteLinkExtractor.MaxTargets);
    }
}
