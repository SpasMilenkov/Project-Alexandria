using Alexandria.Repositories;
using AwesomeAssertions;

namespace Alexandria.Tests.Unit.Repositories;

public class StreamingSearchTermsTests
{
    [Fact]
    public void Parse_cyrillic_query_keeps_tokens()
    {
        var terms = StreamingSearchTerms.Parse("  Обичам Те ");

        terms.IsEmpty.Should().BeFalse();
        terms.NormalizedQuery.Should().Be("обичам те");
        terms.Tokens.Should().Equal("обичам", "те");
        terms.MetadataTsQuery.Should().Be("обичам:* & те:*");
    }

    [Fact]
    public void Parse_apostrophe_splits_like_postgres_parser()
    {
        var terms = StreamingSearchTerms.Parse("Don't stop");

        terms.Tokens.Should().Equal("don", "t", "stop");
    }

    [Fact]
    public void Parse_punctuation_only_query_is_empty()
    {
        var terms = StreamingSearchTerms.Parse("!? -- ...");

        terms.IsEmpty.Should().BeTrue();
        terms.SearchesLyrics.Should().BeFalse();
        terms.LyricsTsQuery.Should().BeEmpty();
    }

    [Fact]
    public void LyricsTsQuery_multiple_words_builds_phrase_with_prefixed_last_word()
    {
        var terms = StreamingSearchTerms.Parse("never gonna giv");

        terms.SearchesLyrics.Should().BeTrue();
        terms.LyricsTsQuery.Should().Be("never <-> gonna <-> giv:*");
    }

    [Fact]
    public void LyricsTsQuery_short_last_word_is_matched_whole()
    {
        var terms = StreamingSearchTerms.Parse("let it be");

        terms.PrefixesLastLyricsToken.Should().BeFalse();
        terms.LyricsTsQuery.Should().Be("let <-> it <-> be");
    }

    [Fact]
    public void SearchesLyrics_query_shorter_than_minimum_is_skipped()
    {
        StreamingSearchTerms.Parse("u2").SearchesLyrics.Should().BeFalse();
        StreamingSearchTerms.Parse("a b").SearchesLyrics.Should().BeFalse();
        StreamingSearchTerms.Parse("sky").SearchesLyrics.Should().BeTrue();
    }

    [Fact]
    public void Parse_decomposed_accents_are_normalized_to_composed_form()
    {
        var decomposed = "café";

        var terms = StreamingSearchTerms.Parse(decomposed);

        terms.Tokens.Should().Equal("café");
    }
}
