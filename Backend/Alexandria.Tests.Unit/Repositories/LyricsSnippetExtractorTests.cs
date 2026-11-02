using Alexandria.Repositories;
using AwesomeAssertions;

namespace Alexandria.Tests.Unit.Repositories;

public class LyricsSnippetExtractorTests
{
    private const string Plain = """
        First verse opens here
        We're never gonna give you up
        Never gonna let you
        down tonight
        """;

    [Fact]
    public void Extract_phrase_in_plain_lyrics_returns_matching_line()
    {
        var snippet = LyricsSnippetExtractor.Extract(Plain, null, StreamingSearchTerms.Parse("gonna giv"));

        snippet.Should().Be("We're never gonna give you up");
    }

    [Fact]
    public void Extract_synced_only_lyrics_strips_lrc_tags()
    {
        var synced = "[ar: Someone]\n[00:12.30] Hello from the other side\n[00:15.00] Second line";

        var snippet = LyricsSnippetExtractor.Extract(null, synced, StreamingSearchTerms.Parse("other side"));

        snippet.Should().Be("Hello from the other side");
    }

    [Fact]
    public void Extract_blank_plain_lyrics_falls_back_to_synced()
    {
        var snippet = LyricsSnippetExtractor.Extract(
            "   ", "[00:01.00] Only synced words", StreamingSearchTerms.Parse("synced"));

        snippet.Should().Be("Only synced words");
    }

    [Fact]
    public void Extract_phrase_wrapping_onto_next_line_returns_both_lines()
    {
        var snippet = LyricsSnippetExtractor.Extract(Plain, null, StreamingSearchTerms.Parse("let you down"));

        snippet.Should().Be("Never gonna let you / down tonight");
    }

    [Fact]
    public void Extract_short_last_word_is_not_prefix_matched()
    {
        var lyrics = "Let it bend\nLet it be";

        var snippet = LyricsSnippetExtractor.Extract(lyrics, null, StreamingSearchTerms.Parse("let it be"));

        snippet.Should().Be("Let it be");
    }

    [Fact]
    public void Extract_cyrillic_query_matches_case_insensitively()
    {
        var lyrics = "Първи ред\nОбичам те до луда";

        var snippet = LyricsSnippetExtractor.Extract(lyrics, null, StreamingSearchTerms.Parse("обичам те"));

        snippet.Should().Be("Обичам те до луда");
    }

    [Fact]
    public void Extract_without_phrase_falls_back_to_line_with_any_token()
    {
        var snippet = LyricsSnippetExtractor.Extract(Plain, null, StreamingSearchTerms.Parse("verse tonight"));

        snippet.Should().Be("First verse opens here");
    }

    [Fact]
    public void Extract_no_matching_word_returns_null()
    {
        var snippet = LyricsSnippetExtractor.Extract(Plain, null, StreamingSearchTerms.Parse("harbor"));

        snippet.Should().BeNull();
    }

    [Fact]
    public void Extract_long_line_is_truncated()
    {
        var lyrics = $"needle {new string('x', 300)}";

        var snippet = LyricsSnippetExtractor.Extract(lyrics, null, StreamingSearchTerms.Parse("needle"));

        snippet.Should().NotBeNull();
        snippet!.Length.Should().Be(LyricsSnippetExtractor.MaxSnippetLength);
        snippet.Should().StartWith("needle ").And.EndWith("…");
    }
}
