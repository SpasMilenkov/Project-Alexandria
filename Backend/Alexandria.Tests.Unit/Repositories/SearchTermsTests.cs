using Alexandria.Repositories.Search;
using AwesomeAssertions;

namespace Alexandria.Tests.Unit.Repositories;

public class SearchTermsTests
{
    [Fact]
    public void Parse_cyrillic_query_keeps_tokens()
    {
        var terms = SearchTerms.Parse("  Обичам Те ");

        terms.IsEmpty.Should().BeFalse();
        terms.NormalizedQuery.Should().Be("обичам те");
        terms.Tokens.Should().Equal("обичам", "те");
        terms.PrefixTsQuery.Should().Be("обичам:* & те:*");
    }

    [Fact]
    public void Parse_apostrophe_splits_like_postgres_parser()
    {
        var terms = SearchTerms.Parse("Don't stop");

        terms.Tokens.Should().Equal("don", "t", "stop");
    }

    [Fact]
    public void Parse_punctuation_only_query_is_empty()
    {
        var terms = SearchTerms.Parse("!? -- ...");

        terms.IsEmpty.Should().BeTrue();
        terms.SearchesLyrics.Should().BeFalse();
        terms.LyricsTsQuery.Should().BeEmpty();
    }

    [Fact]
    public void LyricsTsQuery_multiple_words_builds_phrase_with_prefixed_last_word()
    {
        var terms = SearchTerms.Parse("never gonna giv");

        terms.SearchesLyrics.Should().BeTrue();
        terms.LyricsTsQuery.Should().Be("never <-> gonna <-> giv:*");
    }

    [Fact]
    public void LyricsTsQuery_short_last_word_is_matched_whole()
    {
        var terms = SearchTerms.Parse("let it be");

        terms.PrefixesLastLyricsToken.Should().BeFalse();
        terms.LyricsTsQuery.Should().Be("let <-> it <-> be");
    }

    [Fact]
    public void SearchesLyrics_query_shorter_than_minimum_is_skipped()
    {
        SearchTerms.Parse("u2").SearchesLyrics.Should().BeFalse();
        SearchTerms.Parse("a b").SearchesLyrics.Should().BeFalse();
        SearchTerms.Parse("sky").SearchesLyrics.Should().BeTrue();
    }

    [Fact]
    public void TrigramText_joins_tokens_with_single_spaces_like_stored_names()
    {
        var terms = SearchTerms.Parse("Photos-2024 (Paris)");

        terms.TrigramText.Should().Be("photos 2024 paris");
    }

    [Fact]
    public void ParseOptional_blank_text_means_no_text_filter()
    {
        SearchTerms.ParseOptional(null).Should().BeNull();
        SearchTerms.ParseOptional("   ").Should().BeNull();
        SearchTerms.ParseOptional("--")!.IsEmpty.Should().BeTrue();
    }

    [Fact]
    public void Parse_decomposed_accents_are_normalized_to_composed_form()
    {
        var decomposed = "café";

        var terms = SearchTerms.Parse(decomposed);

        terms.Tokens.Should().Equal("café");
    }
}