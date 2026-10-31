using Alexandria.Common.Mime;
using Alexandria.Common.Policies;
using AwesomeAssertions;

namespace Alexandria.Tests.Unit;

public class FileGroupsTests
{
    [Theory]
    [InlineData(FileGroups.Documents, "application/epub+zip")]
    [InlineData(FileGroups.Documents, "application/vnd.oasis.opendocument.text")]
    [InlineData(FileGroups.Documents, "application/rtf")]
    [InlineData(FileGroups.Spreadsheets, "application/vnd.oasis.opendocument.spreadsheet")]
    [InlineData(FileGroups.Spreadsheets, "text/tab-separated-values")]
    [InlineData(FileGroups.Presentations, "application/vnd.oasis.opendocument.presentation")]
    [InlineData(FileGroups.Archives, "application/java-archive")]
    [InlineData(FileGroups.Archives, "application/vnd.android.package-archive")]
    [InlineData(FileGroups.Archives, "application/x-zip-compressed")]
    [InlineData(FileGroups.Archives, "application/x-xz")]
    [InlineData(FileGroups.Archives, "application/x-bzip2")]
    [InlineData(FileGroups.Code, "text/javascript")]
    [InlineData(FileGroups.Code, "application/javascript")]
    [InlineData(FileGroups.Code, "text/typescript")]
    [InlineData(FileGroups.Code, "application/typescript")]
    [InlineData(FileGroups.Code, "text/x-csharp")]
    [InlineData(FileGroups.Code, "text/x-vue")]
    [InlineData(FileGroups.Code, "text/x-go")]
    [InlineData(FileGroups.Code, "text/x-rust")]
    [InlineData(FileGroups.Code, "text/x-dockerfile")]
    [InlineData(FileGroups.Code, "text/x-containerfile")]
    [InlineData(FileGroups.Code, "text/x-makefile")]
    [InlineData(FileGroups.Code, "text/x-tex")]
    [InlineData(FileGroups.Code, "application/x-tex")]
    [InlineData(FileGroups.Code, "text/x-sln")]
    [InlineData(FileGroups.Code, "text/x-slnx")]
    [InlineData(FileGroups.Code, "text/x-swift")]
    [InlineData(FileGroups.Code, "text/x-scala")]
    [InlineData(FileGroups.Code, "text/x-kotlin")]
    [InlineData(FileGroups.Code, "text/x-kotlin-script")]
    [InlineData(FileGroups.Code, "application/x-php")]
    [InlineData(FileGroups.Code, "text/x-php")]
    [InlineData(FileGroups.Code, "application/x-httpd-php")]
    [InlineData(FileGroups.Code, "application/dart")]
    [InlineData(FileGroups.Code, "text/x-dart")]
    [InlineData(FileGroups.Code, "application/sql")]
    [InlineData(FileGroups.Code, "application/x-sh")]
    [InlineData(FileGroups.Code, "application/xml")]
    [InlineData(FileGroups.Code, "text/xml")]
    [InlineData(FileGroups.Code, "text/x-powershell")]
    [InlineData(FileGroups.Code, "text/x-batch")]
    [InlineData(FileGroups.Code, "text/x-ruby")]
    [InlineData(FileGroups.Code, "text/x-lua")]
    [InlineData(FileGroups.Code, "text/x-r")]
    [InlineData(FileGroups.Code, "text/x-julia")]
    [InlineData(FileGroups.Code, "text/x-fsharp")]
    [InlineData(FileGroups.Code, "text/x-vb")]
    [InlineData(FileGroups.Code, "text/x-scss")]
    [InlineData(FileGroups.Code, "text/x-sass")]
    [InlineData(FileGroups.Code, "text/x-less")]
    [InlineData(FileGroups.Code, "text/x-hcl")]
    [InlineData(FileGroups.Code, "text/x-nix")]
    [InlineData(FileGroups.Code, "text/x-cmake")]
    [InlineData(FileGroups.Code, "text/x-bazel")]
    [InlineData(FileGroups.Code, "text/x-gradle")]
    [InlineData(FileGroups.Code, "text/x-groovy")]
    [InlineData(FileGroups.Code, "application/jsonc")]
    [InlineData(FileGroups.Code, "application/json5")]
    [InlineData(FileGroups.Code, "application/x-ndjson")]
    [InlineData(FileGroups.Code, "text/x-diff")]
    [InlineData(FileGroups.Code, "text/x-htaccess")]
    [InlineData(FileGroups.Code, "text/x-nginx")]
    [InlineData(FileGroups.Text, "text/plain")]
    [InlineData(FileGroups.Text, "text/markdown")]
    [InlineData(FileGroups.Text, "text/x-log")]
    [InlineData(FileGroups.Text, "text/x-srt")]
    [InlineData(FileGroups.Text, "text/x-vtt")]
    [InlineData(FileGroups.Text, "text/x-ass")]
    [InlineData(FileGroups.Text, "text/x-m3u")]
    [InlineData(FileGroups.Text, "text/x-m3u8")]
    [InlineData(FileGroups.Text, "text/x-cue")]
    [InlineData(FileGroups.Text, "text/x-lrc")]
    [InlineData(FileGroups.Text, "text/x-nfo")]
    [InlineData(FileGroups.Text, "text/x-gitignore")]
    [InlineData(FileGroups.Text, "text/x-gitattributes")]
    [InlineData(FileGroups.Text, "text/x-dockerignore")]
    [InlineData(FileGroups.Text, "text/x-editorconfig")]
    public void MatchesMimeType_covers_types_added_by_content_sniffing(string group, string mimeType)
    {
        FileGroups.MatchesMimeType(group, mimeType).Should().BeTrue();
    }

    [Theory]
    [InlineData(FileGroups.Code, "application/x-rar-compressed")]
    [InlineData(FileGroups.Code, "application/x-sharedlib")]
    [InlineData(FileGroups.Code, "application/x-shockwave-flash")]
    [InlineData(FileGroups.Code, "application/x-texinfo")]
    [InlineData(FileGroups.Code, "text/x-cue")]
    [InlineData(FileGroups.Spreadsheets, "text/csv-schema")]
    public void MatchesMimeType_shorter_entry_does_not_claim_a_longer_unrelated_type(string group, string mimeType)
    {
        FileGroups.MatchesMimeType(group, mimeType).Should().BeFalse();
    }

    [Theory]
    [InlineData(FileGroups.Archives, "application/x-rar-compressed")]
    [InlineData(FileGroups.Archives, "application/vnd.rar")]
    [InlineData(FileGroups.Archives, "application/x-7z-compressed")]
    [InlineData(FileGroups.Documents, "application/vnd.openxmlformats-officedocument.wordprocessingml.document")]
    [InlineData(FileGroups.Spreadsheets, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet")]
    [InlineData(FileGroups.Spreadsheets, "application/vnd.ms-excel.sheet.macroEnabled.12")]
    [InlineData(FileGroups.Presentations, "application/vnd.openxmlformats-officedocument.presentationml.presentation")]
    [InlineData(FileGroups.Code, "text/x-csrc")]
    [InlineData(FileGroups.Code, "text/x-c++hdr")]
    [InlineData(FileGroups.Code, "application/x-shellscript")]
    [InlineData(FileGroups.Code, "application/yaml")]
    [InlineData(FileGroups.Code, "text/yaml")]
    [InlineData(FileGroups.Code, "application/toml")]
    [InlineData(FileGroups.Text, "audio/x-mpegurl")]
    [InlineData(FileGroups.Images, "image/avif")]
    [InlineData(FileGroups.Audio, "audio/mp4")]
    public void MatchesMimeType_canonical_types_and_their_aliases_match(string group, string mimeType)
    {
        FileGroups.MatchesMimeType(group, mimeType).Should().BeTrue();
    }

    [Fact]
    public void MatchesMimeType_aliases_leave_their_old_group()
    {
        FileGroups.MatchesMimeType(FileGroups.Audio, "audio/x-mpegurl").Should().BeFalse();
    }

    [Fact]
    public void Groups_list_only_canonical_types_and_slash_terminated_prefixes()
    {
        foreach (var (name, matcher) in FileGroups.All)
        {
            foreach (var type in matcher.Types)
            {
                MimeAliases.Canonicalize(type).Should().Be(type, $"{name} lists {type}");
                MimeAliases.HasValidShape(type).Should().BeTrue($"{name} lists {type}");
            }

            matcher.Prefixes.Should().AllSatisfy(prefix => prefix.Should().EndWith("/"));
        }
    }

    [Fact]
    public void Groups_are_disjoint()
    {
        foreach (var (name, matcher) in FileGroups.All)
        foreach (var type in matcher.Types)
        {
            var owners = FileGroups.All.Where(g => g.Matcher.Matches(type)).Select(g => g.Name);
            owners.Should().Equal([name], $"{type} must belong to exactly one group");
        }
    }

    [Fact]
    public void MatchesMimeType_parameters_are_ignored()
    {
        FileGroups.MatchesMimeType(FileGroups.Text, "text/plain; charset=utf-8").Should().BeTrue();
        FileGroups.MatchesMimeType(FileGroups.Code, " application/json ;charset=utf-8").Should().BeTrue();
    }

    [Fact]
    public void MatchesMimeType_blank_type_matches_nothing()
    {
        FileGroups.MatchesMimeType(FileGroups.Text, "").Should().BeFalse();
        FileGroups.MatchesMimeType(FileGroups.Text, "  ").Should().BeFalse();
    }

    [Fact]
    public void MatchesMimeType_keeps_code_out_of_plain_text()
    {
        FileGroups.MatchesMimeType(FileGroups.Text, "text/x-python").Should().BeFalse();
        FileGroups.MatchesMimeType(FileGroups.Text, "text/x-csharp").Should().BeFalse();
        FileGroups.MatchesMimeType(FileGroups.Code, "text/plain").Should().BeFalse();
    }

    [Fact]
    public void MatchesMimeType_is_case_insensitive()
    {
        FileGroups.MatchesMimeType(FileGroups.Code, "TEXT/X-CSHARP").Should().BeTrue();
        FileGroups.MatchesMimeType(FileGroups.Archives, "APPLICATION/JAVA-ARCHIVE").Should().BeTrue();
    }

    [Fact]
    public void MatchesMimeType_accepts_multiple_semicolon_separated_groups()
    {
        FileGroups.MatchesMimeType($"{FileGroups.Images};{FileGroups.Code}", "text/x-csharp").Should().BeTrue();
        FileGroups.MatchesMimeType($"{FileGroups.Images};{FileGroups.Text}", "text/x-csharp").Should().BeFalse();
    }
}
