using System.Text;
using System.Text.Json;
using Alexandria.Dto.Files;
using Alexandria.Services.Preview.Text;
using Xunit;

namespace Alexandria.Tests.Unit.PreviewJobs;

public class TextPreviewServiceTests
{
    private readonly TextPreviewService sut = new();

    private static MemoryStream Stream(byte[] bytes) => new(bytes);
    private static MemoryStream Stream(string text) => new(Encoding.UTF8.GetBytes(text));

    private static string TextOf(TextPreviewDto preview)
        => string.Concat(preview.Lines.Select(line => line.Text));

    private static bool HasToken(TextPreviewDto preview, string kind, string value)
        => preview.Lines.Any(line => line.Tokens.Any(token =>
            token.Kind == kind && line.Text.Substring(token.Start, token.Length) == value));

    private Task<TextPreviewDto?> Preview(
        Stream stream,
        string fileName,
        long? totalBytes = null,
        int maxBytes = TextPreviewService.DefaultMaxBytes,
        int maxLines = TextPreviewService.DefaultMaxLines,
        int maxChars = TextPreviewService.DefaultMaxChars)
        => sut.GenerateHighlightedPreviewAsync(
            stream,
            fileName,
            totalBytes,
            maxBytes,
            maxLines,
            maxChars,
            TestContext.Current.CancellationToken);

    [Fact]
    public async Task Truncation_inside_a_multibyte_character_leaves_no_replacement_character()
    {
        var bytes = Encoding.UTF8.GetBytes(new string('a', 98) + "\u20AC" + "tail");

        var preview = await Preview(Stream(bytes), "a.txt", maxBytes: 100);

        Assert.NotNull(preview);
        Assert.True(preview.Truncated);
        Assert.Equal(TextPreviewTruncation.Bytes, preview.Truncation);

        var text = TextOf(preview);
        Assert.False(text.Contains('\uFFFD'));
        Assert.False(text.Contains('\u20AC'));
    }

    [Fact]
    public async Task Content_of_exactly_max_bytes_is_not_truncated()
    {
        var preview = await Preview(Stream(new string('a', 100)), "a.txt", maxBytes: 100);

        Assert.NotNull(preview);
        Assert.False(preview.Truncated);
        Assert.Equal(TextPreviewTruncation.None, preview.Truncation);
    }

    [Fact]
    public async Task Truncated_preview_ends_on_a_whole_line()
    {
        var text = string.Concat(Enumerable.Range(0, 200).Select(i => $"line {i}\n"));

        var preview = await Preview(Stream(text), "a.txt", maxBytes: 105);

        Assert.NotNull(preview);
        Assert.True(preview.Truncated);
        Assert.Equal(TextPreviewTruncation.Bytes, preview.Truncation);
        Assert.Matches("^line \\d+\n$", preview.Lines[^1].Text);
    }

    [Fact]
    public async Task Utf8_bom_is_stripped()
    {
        var bytes = new byte[] { 0xEF, 0xBB, 0xBF }.Concat(Encoding.UTF8.GetBytes("hi")).ToArray();

        var preview = await Preview(Stream(bytes), "a.txt");

        Assert.NotNull(preview);
        Assert.Equal("utf-8", preview.Encoding);
        Assert.DoesNotContain('\uFEFF', TextOf(preview));
        Assert.Contains("hi", TextOf(preview));
    }

    [Fact]
    public async Task Utf16_with_bom_is_decoded()
    {
        var bytes = new byte[] { 0xFF, 0xFE }.Concat(Encoding.Unicode.GetBytes("hello")).ToArray();

        var preview = await Preview(Stream(bytes), "a.txt");

        Assert.NotNull(preview);
        Assert.Equal("utf-16le", preview.Encoding);
        Assert.Contains("hello", TextOf(preview));
    }

    [Fact]
    public async Task Invalid_utf8_falls_back_to_latin1()
    {
        var preview = await Preview(Stream(new byte[] { 0x63, 0x61, 0x66, 0xE9 }), "a.txt");

        Assert.NotNull(preview);
        Assert.Equal("latin1", preview.Encoding);
        Assert.Contains("caf\u00E9", TextOf(preview));
    }

    [Fact]
    public async Task Binary_content_returns_null()
    {
        Assert.Null(await Preview(Stream(new byte[] { 0x50, 0x4B, 0x00, 0x01, 0x02 }), "a.md"));
    }

    [Fact]
    public async Task High_control_character_ratio_returns_null()
    {
        var bytes = Enumerable.Range(1, 8).Select(i => (byte)i).Concat(Encoding.ASCII.GetBytes("abc")).ToArray();

        Assert.Null(await Preview(Stream(bytes), "a.txt"));
    }

    [Fact]
    public async Task Empty_file_is_an_empty_preview_not_null()
    {
        var preview = await Preview(Stream(Array.Empty<byte>()), "a.txt");

        Assert.NotNull(preview);
        Assert.Empty(preview.Lines);
        Assert.Equal(0, preview.LineCount);
        Assert.Equal(0, preview.TokenCount);
        Assert.Equal(0, preview.DomElementCount);
    }

    [Fact]
    public async Task Language_follows_the_file_name_and_reports_metadata()
    {
        var preview = await Preview(Stream("class A {}\n"), "A.cs", totalBytes: 11);

        Assert.NotNull(preview);
        Assert.Equal("csharp", preview.Language);
        Assert.Equal(11, preview.TotalBytes);
        Assert.Equal(1, preview.LineCount);
        Assert.Equal(preview.LineCount + preview.TokenCount, preview.DomElementCount);
        Assert.True(HasToken(preview, "kw", "class"));
    }

    [Fact]
    public async Task Newline_heavy_input_is_line_capped_and_dom_bounded()
    {
        var text = new string('\n', 512 * 1024);

        var preview = await Preview(
            Stream(text), "a.txt", maxBytes: text.Length, maxChars: text.Length);

        Assert.NotNull(preview);
        Assert.True(preview.Truncated);
        Assert.Equal(TextPreviewTruncation.Lines, preview.Truncation);
        Assert.Equal(TextPreviewService.DefaultMaxLines, preview.LineCount);
        Assert.Equal(0, preview.TokenCount);
        Assert.Equal(TextPreviewService.DefaultMaxLines, preview.DomElementCount);

        var json = JsonSerializer.Serialize(preview, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        Assert.True(Encoding.UTF8.GetByteCount(json) < 200_000);
        Assert.DoesNotContain("\"html\"", json);
    }

    [Fact]
    public async Task Carriage_return_heavy_input_is_line_capped_and_dom_bounded()
    {
        var text = new string('\r', 512 * 1024);

        var preview = await Preview(
            Stream(text), "a.txt", maxBytes: text.Length, maxChars: text.Length);

        Assert.NotNull(preview);
        Assert.True(preview.Truncated);
        Assert.Equal(TextPreviewTruncation.Lines, preview.Truncation);
        Assert.Equal(TextPreviewService.DefaultMaxLines, preview.LineCount);
        Assert.Equal(TextPreviewService.DefaultMaxLines, preview.DomElementCount);
    }

    [Fact]
    public async Task Short_line_heavy_input_is_line_capped_and_dom_bounded()
    {
        var text = string.Concat(Enumerable.Repeat("a\n", 256 * 1024));

        var preview = await Preview(
            Stream(text), "a.txt", maxBytes: text.Length, maxChars: text.Length);

        Assert.NotNull(preview);
        Assert.True(preview.Truncated);
        Assert.Equal(TextPreviewTruncation.Lines, preview.Truncation);
        Assert.Equal(TextPreviewService.DefaultMaxLines, preview.LineCount);
        Assert.Equal(TextPreviewService.DefaultMaxLines, preview.DomElementCount);
    }

    [Fact]
    public async Task Single_long_line_is_character_capped()
    {
        var text = new string('a', 512 * 1024);

        var preview = await Preview(Stream(text), "a.txt");

        Assert.NotNull(preview);
        Assert.True(preview.Truncated);
        Assert.Equal(TextPreviewTruncation.Characters, preview.Truncation);
        Assert.Equal(1, preview.LineCount);
        Assert.Equal(1, preview.DomElementCount);
        Assert.Equal(TextPreviewService.DefaultMaxChars, preview.BytesShown);
    }

    [Fact]
    public async Task Character_limit_above_default_is_clamped()
    {
        var text = new string('a', TextPreviewService.DefaultMaxChars + 50_000);

        var preview = await Preview(
            Stream(text),
            "a.txt",
            maxBytes: int.MaxValue,
            maxLines: int.MaxValue,
            maxChars: int.MaxValue);

        Assert.NotNull(preview);
        Assert.True(preview.Truncated);
        Assert.Equal(TextPreviewTruncation.Characters, preview.Truncation);
        Assert.True(TextOf(preview).Length <= TextPreviewService.DefaultMaxChars + 1);
        Assert.Equal(TextPreviewService.DefaultMaxChars, preview.BytesShown);
    }

    [Fact]
    public async Task Character_limit_does_not_split_a_surrogate_pair()
    {
        var text = new string('a', TextPreviewService.DefaultMaxChars - 1) + "\U0001F600" + "tail";

        var preview = await Preview(Stream(text), "a.txt");

        Assert.NotNull(preview);
        Assert.True(preview.Truncated);
        Assert.Equal(TextPreviewTruncation.Characters, preview.Truncation);
        Assert.DoesNotContain(TextOf(preview), c => char.IsSurrogate(c));
        Assert.Equal(TextPreviewService.DefaultMaxChars - 1, preview.BytesShown);
    }

    [Fact]
    public async Task Line_limit_above_default_is_clamped()
    {
        var text = new string('\n', TextPreviewService.DefaultMaxLines + 100);

        var preview = await Preview(
            Stream(text),
            "a.txt",
            maxLines: int.MaxValue,
            maxChars: int.MaxValue);

        Assert.NotNull(preview);
        Assert.True(preview.Truncated);
        Assert.Equal(TextPreviewTruncation.Lines, preview.Truncation);
        Assert.Equal(TextPreviewService.DefaultMaxLines, preview.LineCount);
        Assert.Equal(TextPreviewService.DefaultMaxLines, preview.DomElementCount);
    }

    [Fact]
    public async Task Token_limit_remains_capped_when_the_line_limit_is_lower()
    {
        var line = string.Concat(Enumerable.Repeat("var a = 1; ", 50)) + "\n";
        var text = string.Concat(Enumerable.Repeat(line, 100));

        var preview = await Preview(
            Stream(text),
            "a.js",
            maxLines: 100,
            maxChars: int.MaxValue);

        Assert.NotNull(preview);
        Assert.True(preview.HighlightingLimited);
        Assert.Equal(100, preview.LineCount);
        Assert.Equal(TextPreviewService.DefaultMaxTokenSpans, preview.TokenCount);
        Assert.True(preview.DomElementCount <= TextPreviewService.DefaultMaxDomElements);
    }

    [Fact]
    public async Task Token_budget_bounds_rendered_elements()
    {
        var text = string.Concat(Enumerable.Repeat(
            "public static async Task<int> Run(string s) { return s.Length + 42; } // c\n", 2_000));

        var preview = await Preview(
            Stream(text), "A.cs", maxBytes: text.Length, maxChars: text.Length);

        Assert.NotNull(preview);
        Assert.True(preview.HighlightingLimited);
        Assert.Equal(TextPreviewService.DefaultMaxTokenSpans, preview.TokenCount);
        Assert.Equal(preview.LineCount + preview.TokenCount, preview.DomElementCount);
        Assert.True(preview.DomElementCount <= TextPreviewService.DefaultMaxDomElements);

        var json = JsonSerializer.Serialize(preview, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        Assert.True(Encoding.UTF8.GetByteCount(json) < 1_000_000);
    }

    [Fact]
    public async Task Server_returns_raw_text_not_escaped_markup()
    {
        var preview = await Preview(Stream("<b>&\n"), "a.txt");

        Assert.NotNull(preview);
        Assert.Equal("<b>&\n", TextOf(preview));
        Assert.DoesNotContain("&lt;", TextOf(preview));
    }
}
