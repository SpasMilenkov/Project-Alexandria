using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using Alexandria.Services.Preview.Text;
using Xunit;

namespace Alexandria.Tests.Unit.PreviewJobs;

public class TextHighlighterTests
{
    private static readonly Regex TagRegex = new(
        "<span class=\"(l|kw|st|cm|nu|li|fn|ty|ky|tg|at|mt|hd|bd)\">|</span>", RegexOptions.Compiled);

    private static readonly Regex BareAmpersand = new("&(?!(amp|lt|gt|quot|#39);)", RegexOptions.Compiled);

    private static string Detag(string html) => WebUtility.HtmlDecode(Regex.Replace(html, "<[^>]*>", ""));

    private static string Expected(string input)
    {
        var normalized = input.Replace("\r\n", "\n").Replace('\r', '\n');
        if (normalized.Length == 0) return "";
        return normalized.EndsWith('\n') ? normalized : normalized + "\n";
    }

    private static void AppendEscaped(StringBuilder sb, string text, int from, int to)
    {
        for (var i = from; i < to; i++)
        {
            sb.Append(text[i] switch
            {
                '&' => "&amp;",
                '<' => "&lt;",
                '>' => "&gt;",
                '"' => "&quot;",
                '\'' => "&#39;",
                _ => text[i].ToString()
            });
        }
    }

    /// <summary>Renders the token model with the old markup shape so lexical assertions stay readable.</summary>
    private static string ToHtml(HighlightResult result)
    {
        var sb = new StringBuilder();

        foreach (var line in result.Lines)
        {
            sb.Append("<span class=\"l\">");
            var position = 0;

            foreach (var token in line.Tokens)
            {
                AppendEscaped(sb, line.Text, position, token.Start);
                sb.Append("<span class=\"").Append(token.Kind).Append("\">");
                AppendEscaped(sb, line.Text, token.Start, token.Start + token.Length);
                sb.Append("</span>");
                position = token.Start + token.Length;
            }

            AppendEscaped(sb, line.Text, position, line.Text.Length);
            sb.Append("</span>");
        }

        return sb.ToString();
    }

    private static void AssertTokenRanges(HighlightResult result)
    {
        foreach (var line in result.Lines)
        {
            var previousEnd = 0;

            foreach (var token in line.Tokens)
            {
                Assert.True(token.Start >= previousEnd);
                Assert.True(token.Length > 0);
                Assert.True(token.Start + token.Length <= line.Text.Length);
                Assert.DoesNotContain('\n', line.Text.Substring(token.Start, token.Length));

                previousEnd = token.Start + token.Length;
            }
        }

        Assert.Equal(result.LineCount + result.TokenCount, result.DomElementCount);
    }

    /// <summary>Renders the test shape and checks its old well-formedness contract.</summary>
    private static void AssertRenderable(HighlightResult result, string input)
    {
        AssertTokenRanges(result);

        var html = ToHtml(result);
        AssertWellFormed(html);
        Assert.Equal(Expected(input), Detag(html));
    }

    /// <summary>The test renderer's shape: only whitelisted spans, balanced, nothing unescaped.</summary>
    private static void AssertWellFormed(string html)
    {
        var depth = 0;
        var position = 0;

        foreach (Match match in TagRegex.Matches(html))
        {
            var between = html[position..match.Index];
            Assert.False(between.Contains('<') || between.Contains('>'));
            Assert.DoesNotMatch(BareAmpersand, between);

            if (match.Value == "</span>")
            {
                depth--;
                Assert.True(depth >= 0);
            }
            else
            {
                var isLine = match.Groups[1].Value == "l";
                Assert.True(isLine ? depth == 0 : depth == 1);
                depth++;
            }

            position = match.Index + match.Length;
        }

        var tail = html[position..];
        Assert.False(tail.Contains('<') || tail.Contains('>'));
        Assert.Equal(0, depth);
    }

    private static string Run(string input, string language)
    {
        var result = TextHighlighter.Highlight(input, PreviewLanguages.ByName(language));
        AssertRenderable(result, input);
        return ToHtml(result);
    }

    [Fact]
    public void Json_marks_keys_strings_numbers_and_literals()
    {
        var html = Run("{\"name\": \"x\", \"n\": 12, \"ok\": true}", "json");

        Assert.Contains("<span class=\"ky\">&quot;name&quot;</span>", html);
        Assert.Contains("<span class=\"st\">&quot;x&quot;</span>", html);
        Assert.Contains("<span class=\"nu\">12</span>", html);
        Assert.Contains("<span class=\"li\">true</span>", html);
    }

    [Fact]
    public void CSharp_marks_keywords_types_numbers_and_comments()
    {
        var html = Run("public class Foo { int x = 1; // hi\n}", "csharp");

        Assert.Contains("<span class=\"kw\">public</span>", html);
        Assert.Contains("<span class=\"kw\">class</span>", html);
        Assert.Contains("<span class=\"ty\">Foo</span>", html);
        Assert.Contains("<span class=\"nu\">1</span>", html);
        Assert.Contains("<span class=\"cm\">// hi</span>", html);
    }

    [Fact]
    public void CSharp_verbatim_and_interpolated_strings()
    {
        var html = Run("var a = @\"C:\\dir\"\"q\"; var b = $\"x{y}\";", "csharp");

        Assert.Contains("<span class=\"st\">@&quot;C:\\dir&quot;&quot;q&quot;</span>", html);
        Assert.Contains("<span class=\"st\">$&quot;x{y}&quot;</span>", html);
    }

    [Fact]
    public void Python_triple_quoted_string_is_split_per_line()
    {
        var result = TextHighlighter.Highlight("x = \"\"\"doc\nstring\"\"\"\nprint(x)", PreviewLanguages.Python);
        AssertRenderable(result, "x = \"\"\"doc\nstring\"\"\"\nprint(x)");

        var html = ToHtml(result);
        Assert.Equal(3, result.LineCount);
        Assert.Equal(2, Regex.Matches(html, "class=\"st\"").Count);
        Assert.Contains("<span class=\"fn\">print</span>", html);
    }

    [Fact]
    public void Python_string_prefixes_belong_to_the_string()
    {
        var html = Run("a = f'x' + rb\"y\"", "python");

        Assert.Contains("<span class=\"st\">f&#39;x&#39;</span>", html);
        Assert.Contains("<span class=\"st\">rb&quot;y&quot;</span>", html);
    }

    [Fact]
    public void Yaml_keys_and_apostrophes()
    {
        var html = Run("name: demo\nurl: http://x.io/a\nnote: don't stop # tail\n", "yaml");

        Assert.Contains("<span class=\"ky\">name</span>", html);
        Assert.Contains("<span class=\"ky\">url</span>", html);
        Assert.DoesNotContain("<span class=\"ky\">http</span>", html);
        Assert.DoesNotContain("class=\"st\"", html);
        Assert.Contains("<span class=\"cm\"># tail</span>", html);
    }

    [Fact]
    public void Sql_is_case_insensitive()
    {
        var html = Run("SELECT id FROM users where name = 'it''s' -- c", "sql");

        Assert.Contains("<span class=\"kw\">SELECT</span>", html);
        Assert.Contains("<span class=\"kw\">where</span>", html);
        Assert.Contains("<span class=\"st\">&#39;it&#39;&#39;s&#39;</span>", html);
        Assert.Contains("<span class=\"cm\">-- c</span>", html);
    }

    [Fact]
    public void Css_keys_only_inside_braces()
    {
        var html = Run("a:hover { color: #fff; margin: 10px }", "css");

        Assert.DoesNotContain("<span class=\"ky\">a</span>", html);
        Assert.Contains("<span class=\"ky\">color</span>", html);
        Assert.Contains("<span class=\"nu\">#fff</span>", html);
        Assert.Contains("<span class=\"nu\">10px</span>", html);
    }

    [Fact]
    public void Bash_variables_and_shebang()
    {
        var html = Run("#!/bin/bash\nif [ \"$1\" = x ]; then echo ${HOME}; fi", "bash");

        Assert.Contains("<span class=\"cm\">#!/bin/bash</span>", html);
        Assert.Contains("<span class=\"kw\">if</span>", html);
        Assert.Contains("<span class=\"at\">${HOME}</span>", html);
    }

    [Fact]
    public void Markdown_highlights_fenced_code_by_language()
    {
        var html = Run("# Title\n\ntext `code` and **bold**\n\n```json\n{\"a\": 1}\n```\n- item\n", "markdown");

        Assert.Contains("<span class=\"hd\"># Title</span>", html);
        Assert.Contains("<span class=\"st\">`code`</span>", html);
        Assert.Contains("<span class=\"bd\">**bold**</span>", html);
        Assert.Contains("<span class=\"ky\">&quot;a&quot;</span>", html);
        Assert.Contains("<span class=\"mt\">```json</span>", html);
        Assert.Contains("<span class=\"mt\">-</span>", html);
    }

    [Fact]
    public void Markdown_unterminated_fence_does_not_swallow_or_lose_text()
    {
        Run("```cs\nint x = 1;\nno closing fence", "markdown");
    }

    [Fact]
    public void Vue_highlights_template_script_and_style()
    {
        var input = "<template><div class=\"a\">x</div></template>\n<script setup lang=\"ts\">\nconst a: number = 1;\n</script>\n<style>\n.a { color: red; }\n</style>\n";
        var html = Run(input, "vue");

        Assert.Contains("<span class=\"tg\">div</span>", html);
        Assert.Contains("<span class=\"at\">class</span>", html);
        Assert.Contains("<span class=\"kw\">const</span>", html);
        Assert.Contains("<span class=\"ky\">color</span>", html);
    }

    [Fact]
    public void Makefile_highlights_directives_variables_and_comments()
    {
        var html = Run("include config.mk\nall: app\n\t$(CC) -o app main.c # build\n", "makefile");

        Assert.Contains("<span class=\"kw\">include</span>", html);
        Assert.Contains("<span class=\"at\">$(CC)</span>", html);
        Assert.Contains("<span class=\"cm\"># build</span>", html);
    }

    [Fact]
    public void Log_highlights_levels()
    {
        var html = Run("2026-10-27 12:00:00 ERROR failed in 12ms\n", "log");

        Assert.Contains("<span class=\"kw\">ERROR</span>", html);
        Assert.Contains("<span class=\"nu\">2026</span>", html);
    }

    [Fact]
    public void Tex_highlights_commands_and_comments()
    {
        var html = Run("\\section{Intro} % note\n", "tex");

        Assert.Contains("<span class=\"kw\">section</span>", html);
        Assert.Contains("<span class=\"cm\">% note</span>", html);
    }

    [Fact]
    public void Sln_highlights_keys_keywords_and_strings()
    {
        var html = Run(
            "VisualStudioVersion = 17.0\nProject(\"{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}\") = \"App\", \"App.csproj\"\n",
            "sln");

        Assert.Contains("<span class=\"ky\">VisualStudioVersion</span>", html);
        Assert.Contains("<span class=\"kw\">Project</span>", html);
        Assert.Contains("<span class=\"st\">&quot;App&quot;</span>", html);
    }

    [Fact]
    public void Swift_highlights_keywords_strings_and_comments()
    {
        var html = Run("let name = \"Ada\" // hi\n", "swift");

        Assert.Contains("<span class=\"kw\">let</span>", html);
        Assert.Contains("<span class=\"st\">&quot;Ada&quot;</span>", html);
        Assert.Contains("<span class=\"cm\">// hi</span>", html);
    }

    [Fact]
    public void Scala_highlights_keywords_and_types()
    {
        var html = Run("object Main extends App { val x = 1 }\n", "scala");

        Assert.Contains("<span class=\"kw\">object</span>", html);
        Assert.Contains("<span class=\"kw\">extends</span>", html);
        Assert.Contains("<span class=\"kw\">val</span>", html);
        Assert.Contains("<span class=\"nu\">1</span>", html);
    }

    [Fact]
    public void Php_highlights_variables_strings_and_comments()
    {
        var html = Run("<?php $name = \"Ada\"; // hi\n", "php");

        Assert.Contains("<span class=\"at\">$name</span>", html);
        Assert.Contains("<span class=\"st\">&quot;Ada&quot;</span>", html);
        Assert.Contains("<span class=\"cm\">// hi</span>", html);
    }

    [Fact]
    public void Kotlin_highlights_keywords_types_and_comments()
    {
        var html = Run("data class User(val name: String) // hi\n", "kotlin");

        Assert.Contains("<span class=\"kw\">data</span>", html);
        Assert.Contains("<span class=\"kw\">class</span>", html);
        Assert.Contains("<span class=\"kw\">val</span>", html);
        Assert.Contains("<span class=\"cm\">// hi</span>", html);
    }

    [Fact]
    public void Dart_highlights_keywords_strings_and_comments()
    {
        var html = Run("class User { final String name = \"Ada\"; } // hi\n", "dart");

        Assert.Contains("<span class=\"kw\">class</span>", html);
        Assert.Contains("<span class=\"kw\">final</span>", html);
        Assert.Contains("<span class=\"st\">&quot;Ada&quot;</span>", html);
        Assert.Contains("<span class=\"cm\">// hi</span>", html);
    }

    [Fact]
    public void Csv_highlights_delimiters_and_quoted_fields()
    {
        var html = Run("name,notes\n\"Ada, A.\",ok\n", "csv");

        Assert.Contains("<span class=\"mt\">,</span>", html);
        Assert.Contains("<span class=\"st\">&quot;Ada, A.&quot;</span>", html);
    }

    [Fact]
    public void PowerShell_highlights_keywords_variables_and_block_comments()
    {
        var html = Run("function Get-X { param($Name) $Name } # note\n<# block #>\n", "powershell");

        Assert.Contains("<span class=\"kw\">function</span>", html);
        Assert.Contains("<span class=\"at\">$Name</span>", html);
        Assert.Contains("<span class=\"cm\"># note</span>", html);
        Assert.Contains("<span class=\"cm\">&lt;# block #&gt;</span>", html);
    }

    [Fact]
    public void Batch_highlights_keywords_percent_variables_and_labels()
    {
        var html = Run("@echo off\nset NAME=world\necho %NAME% :: note\n", "batch");

        Assert.Contains("<span class=\"kw\">echo</span>", html);
        Assert.Contains("<span class=\"kw\">set</span>", html);
        Assert.Contains("<span class=\"at\">%NAME%</span>", html);
        Assert.Contains("<span class=\"cm\">:: note</span>", html);
    }

    [Fact]
    public void Ruby_highlights_keywords_methods_and_comments()
    {
        var html = Run("def greet(name)\n  puts \"hi\" # note\nend\n", "ruby");

        Assert.Contains("<span class=\"kw\">def</span>", html);
        Assert.Contains("<span class=\"fn\">greet</span>", html);
        Assert.Contains("<span class=\"st\">&quot;hi&quot;</span>", html);
        Assert.Contains("<span class=\"cm\"># note</span>", html);
    }

    [Fact]
    public void Lua_highlights_keywords_and_long_comments()
    {
        var html = Run("--[[ note ]]\nlocal x = 1\n", "lua");

        Assert.Contains("<span class=\"cm\">--[[ note ]]</span>", html);
        Assert.Contains("<span class=\"kw\">local</span>", html);
        Assert.Contains("<span class=\"nu\">1</span>", html);
    }

    [Fact]
    public void R_highlights_functions_and_comments()
    {
        var html = Run("f <- function(x) { x + 1 } # note\n", "r");

        Assert.Contains("<span class=\"kw\">function</span>", html);
        Assert.Contains("<span class=\"nu\">1</span>", html);
        Assert.Contains("<span class=\"cm\"># note</span>", html);
    }

    [Fact]
    public void Julia_highlights_block_comments_before_line_comments()
    {
        var html = Run("#= block =#\nfunction f(x)\n  1\nend # note\n", "julia");

        Assert.Contains("<span class=\"cm\">#= block =#</span>", html);
        Assert.Contains("<span class=\"kw\">function</span>", html);
        Assert.Contains("<span class=\"cm\"># note</span>", html);
    }

    [Fact]
    public void FSharp_highlights_keywords_strings_and_nested_comment_markers()
    {
        var html = Run("let name = \"Ada\" (* note *)\n", "fsharp");

        Assert.Contains("<span class=\"kw\">let</span>", html);
        Assert.Contains("<span class=\"st\">&quot;Ada&quot;</span>", html);
        Assert.Contains("<span class=\"cm\">(* note *)</span>", html);
    }

    [Fact]
    public void VisualBasic_highlights_case_insensitive_keywords_and_comments()
    {
        var html = Run("Dim Name As String = \"Ada\" ' note\n", "vb");

        Assert.Contains("<span class=\"kw\">Dim</span>", html);
        Assert.Contains("<span class=\"kw\">As</span>", html);
        Assert.Contains("<span class=\"st\">&quot;Ada&quot;</span>", html);
        Assert.Contains("<span class=\"cm\">&#39; note</span>", html);
    }

    [Fact]
    public void Stylesheet_dialects_highlight_variables_comments_and_colors()
    {
        var scss = Run("$color: #fff; // note\n.a { color: $color; }\n", "scss");
        Assert.Contains("<span class=\"at\">$color</span>", scss);
        Assert.Contains("<span class=\"nu\">#fff</span>", scss);
        Assert.Contains("<span class=\"cm\">// note</span>", scss);

        var less = Run("@color: #000;\n.a { color: @color; }\n", "less");
        Assert.Contains("<span class=\"kw\">@color</span>", less);
    }

    [Fact]
    public void Hcl_highlights_keys_variables_and_comments()
    {
        var html = Run("name = ${var.value} # note\n", "hcl");

        Assert.Contains("<span class=\"ky\">name</span>", html);
        Assert.Contains("<span class=\"at\">${var.value}</span>", html);
        Assert.Contains("<span class=\"cm\"># note</span>", html);
    }

    [Fact]
    public void Nix_and_cmake_highlight_keywords_and_comments()
    {
        var nix = Run("let x = 1; in x # note\n", "nix");
        Assert.Contains("<span class=\"kw\">let</span>", nix);
        Assert.Contains("<span class=\"kw\">in</span>", nix);
        Assert.Contains("<span class=\"cm\"># note</span>", nix);

        var cmake = Run("set(VAR \"x\") # note\n", "cmake");
        Assert.Contains("<span class=\"kw\">set</span>", cmake);
        Assert.Contains("<span class=\"st\">&quot;x&quot;</span>", cmake);
        Assert.Contains("<span class=\"cm\"># note</span>", cmake);
    }

    [Fact]
    public void Bazel_and_gradle_highlight_build_scripts()
    {
        var bazel = Run("load(\"//x:y.bzl\", \"x\")\n", "bazel");
        Assert.Contains("<span class=\"kw\">load</span>", bazel);
        Assert.Contains("<span class=\"st\">&quot;//x:y.bzl&quot;</span>", bazel);

        var gradle = Run("plugins { id(\"java\") }\n", "gradle");
        Assert.Contains("<span class=\"kw\">plugins</span>", gradle);
        Assert.Contains("<span class=\"fn\">id</span>", gradle);
        Assert.Contains("<span class=\"st\">&quot;java&quot;</span>", gradle);
    }

    [Fact]
    public void Diff_marks_headers_hunks_and_changed_lines()
    {
        var html = Run("diff --git a/x b/x\n@@ -1 +1 @@\n-old\n+new\n", "diff");

        Assert.Contains("<span class=\"mt\">diff --git a/x b/x</span>", html);
        Assert.Contains("<span class=\"hd\">@@ -1 +1 @@</span>", html);
        Assert.Contains("<span class=\"kw\">-old</span>", html);
        Assert.Contains("<span class=\"st\">+new</span>", html);
    }

    [Fact]
    public void Json5_accepts_single_quoted_keys_and_strings()
    {
        var html = Run("{name: 'x', n: 0x10}", "json5");

        Assert.Contains("<span class=\"ky\">name</span>", html);
        Assert.Contains("<span class=\"st\">&#39;x&#39;</span>", html);
        Assert.Contains("<span class=\"nu\">0x10</span>", html);
    }

    [Fact]
    public void Subtitle_formats_highlight_cues_and_timing_lines()
    {
        var srt = Run("1\n00:00:01,000 --> 00:00:04,000\nHello\n", "srt");
        Assert.Contains("<span class=\"nu\">1</span>", srt);
        Assert.Contains("<span class=\"mt\">00:00:01,000 --&gt; 00:00:04,000</span>", srt);

        var vtt = Run("WEBVTT\n\nNOTE license\n00:00.000 --> 00:02.000\nHello\n", "vtt");
        Assert.Contains("<span class=\"kw\">WEBVTT</span>", vtt);
        Assert.Contains("<span class=\"cm\">NOTE license</span>", vtt);
        Assert.Contains("<span class=\"mt\">00:00.000 --&gt; 00:02.000</span>", vtt);
    }

    [Fact]
    public void Ass_and_lrc_highlight_sections_and_keys()
    {
        var ass = Run("[Events]\nDialogue: 0,0:00:00.00,0:00:05.00,Default,,0,0,0,,Hello\n", "ass");
        Assert.Contains("<span class=\"mt\">[Events]</span>", ass);
        Assert.Contains("<span class=\"ky\">Dialogue</span>", ass);
        Assert.Contains("<span class=\"nu\">0</span>", ass);

        var lrc = Run("[ar:Artist]\n[00:12.34]Hello\n", "lrc");
        Assert.Contains("<span class=\"mt\">[ar:Artist]</span>", lrc);
        Assert.Contains("<span class=\"mt\">[00:12.34]</span>", lrc);
    }

    [Fact]
    public void Playlist_cue_and_nfo_formats_highlight_their_metadata()
    {
        var m3u = Run("#EXTM3U\n#EXTINF:123,Artist - Song\nmusic/song.mp3\n", "m3u");
        Assert.Contains("<span class=\"cm\">#EXTM3U</span>", m3u);
        Assert.Contains("<span class=\"cm\">#EXTINF:123,Artist - Song</span>", m3u);

        var cue = Run("REM comment\nPERFORMER \"Artist\"\nTRACK 01 AUDIO\nINDEX 01 00:00:00\n", "cue");
        Assert.Contains("<span class=\"cm\">REM comment</span>", cue);
        Assert.Contains("<span class=\"kw\">PERFORMER</span>", cue);
        Assert.Contains("<span class=\"st\">&quot;Artist&quot;</span>", cue);
        Assert.Contains("<span class=\"li\">AUDIO</span>", cue);

        var nfo = Run("just text\n", "nfo");
        Assert.DoesNotContain("<span class=\"kw\"", nfo);
        Assert.Contains("just text", nfo);
    }

    [Fact]
    public void Ignore_and_server_configs_highlight_directives_and_variables()
    {
        var ignore = Run("# comment\n*.log\n!keep.log\n", "ignore");
        Assert.Contains("<span class=\"cm\"># comment</span>", ignore);
        Assert.Contains("*.log", ignore);

        var htaccess = Run("RewriteEngine On\nRewriteCond %{HTTP_HOST} ^www\\.example\\.com$ [NC]\n", "htaccess");
        Assert.Contains("<span class=\"kw\">RewriteEngine</span>", htaccess);
        Assert.Contains("<span class=\"li\">On</span>", htaccess);
        Assert.Contains("<span class=\"at\">%{HTTP_HOST}</span>", htaccess);

        var nginx = Run(
            "server {\n  listen 80;\n  server_name example.com;\n  proxy_set_header Host $host;\n}\n",
            "nginx");
        Assert.Contains("<span class=\"kw\">server</span>", nginx);
        Assert.Contains("<span class=\"kw\">listen</span>", nginx);
        Assert.Contains("<span class=\"nu\">80</span>", nginx);
        Assert.Contains("<span class=\"at\">$host</span>", nginx);
    }

    [Theory]
    [InlineData("plaintext")]
    [InlineData("json")]
    [InlineData("javascript")]
    [InlineData("typescript")]
    [InlineData("csharp")]
    [InlineData("python")]
    [InlineData("java")]
    [InlineData("go")]
    [InlineData("rust")]
    [InlineData("c")]
    [InlineData("cpp")]
    [InlineData("php")]
    [InlineData("swift")]
    [InlineData("scala")]
    [InlineData("kotlin")]
    [InlineData("dart")]
    [InlineData("sql")]
    [InlineData("bash")]
    [InlineData("dockerfile")]
    [InlineData("makefile")]
    [InlineData("log")]
    [InlineData("csv")]
    [InlineData("tex")]
    [InlineData("sln")]
    [InlineData("yaml")]
    [InlineData("toml")]
    [InlineData("ini")]
    [InlineData("css")]
    [InlineData("xml")]
    [InlineData("markdown")]
    [InlineData("powershell")]
    [InlineData("batch")]
    [InlineData("ruby")]
    [InlineData("lua")]
    [InlineData("r")]
    [InlineData("julia")]
    [InlineData("fsharp")]
    [InlineData("vb")]
    [InlineData("scss")]
    [InlineData("sass")]
    [InlineData("less")]
    [InlineData("hcl")]
    [InlineData("nix")]
    [InlineData("cmake")]
    [InlineData("bazel")]
    [InlineData("gradle")]
    [InlineData("json5")]
    [InlineData("diff")]
    [InlineData("srt")]
    [InlineData("vtt")]
    [InlineData("ass")]
    [InlineData("m3u")]
    [InlineData("cue")]
    [InlineData("lrc")]
    [InlineData("nfo")]
    [InlineData("ignore")]
    [InlineData("htaccess")]
    [InlineData("nginx")]
    public void Hostile_input_cannot_introduce_markup(string language)
    {
        const string hostile =
            "<img src=x onerror=alert(1)>\"><script>alert(1)</script>\n<svg onload=alert(1)>&amp;&lt;&#39;\n";

        var html = Run(hostile, language);

        Assert.DoesNotContain("<img", html);
        Assert.DoesNotContain("<script", html);
        Assert.DoesNotContain("<svg", html);
        Assert.DoesNotContain("onerror=alert(1)>", html);
    }

    [Fact]
    public void Fuzz_round_trips_and_stays_well_formed_for_every_language()
    {
        string[] languages =
        [
            "plaintext", "json", "javascript", "typescript", "csharp", "python", "java", "go", "rust", "c", "cpp",
            "php", "swift", "scala", "kotlin", "dart", "sql", "bash", "dockerfile", "makefile", "log", "csv", "tex",
            "sln", "yaml", "toml", "ini", "css", "xml", "markdown", "vue", "powershell", "batch", "ruby", "lua", "r",
            "julia", "fsharp", "vb", "scss", "sass", "less", "hcl", "nix", "cmake", "bazel", "gradle", "json5", "diff",
            "srt", "vtt", "ass", "m3u", "cue", "lrc", "nfo", "ignore", "htaccess", "nginx"
        ];
        const string alphabet = "abcXYZ_019 \t\n\n\r\"'`#/*<>&{}[]():=-;$@!\\.,%éλ€";
        var random = new Random(4242);

        foreach (var language in languages)
        {
            for (var round = 0; round < 250; round++)
            {
                var length = random.Next(0, 120);
                var sb = new StringBuilder(length);
                for (var k = 0; k < length; k++) sb.Append(alphabet[random.Next(alphabet.Length)]);

                Run(sb.ToString(), language);
            }
        }
    }

    [Theory]
    [InlineData("javascript")]
    [InlineData("csharp")]
    [InlineData("python")]
    [InlineData("bash")]
    public void Unterminated_string_stops_at_the_end_of_the_line(string language)
    {
        var html = Run("x = \"never closed\ny = 2\nfoo(1)\n", language);

        var secondLine = html[html.IndexOf("y", html.IndexOf("never", StringComparison.Ordinal), StringComparison.Ordinal)..];
        Assert.DoesNotContain("class=\"st\"", secondLine);
    }

    [Fact]
    public void Empty_and_line_counts()
    {
        var empty = TextHighlighter.Highlight("", PreviewLanguages.Plain);
        Assert.Empty(empty.Lines);
        Assert.Equal("", ToHtml(empty));
        Assert.Equal(0, empty.LineCount);
        Assert.Equal(0, empty.TokenCount);
        Assert.Equal(0, empty.DomElementCount);

        Assert.Equal(2, TextHighlighter.Highlight("a\nb\n", PreviewLanguages.Plain).LineCount);
        Assert.Equal(2, TextHighlighter.Highlight("a\nb", PreviewLanguages.Plain).LineCount);
        Assert.Equal(2, TextHighlighter.Highlight("\n\n", PreviewLanguages.Plain).LineCount);
        Assert.Equal(2, TextHighlighter.Highlight("a\r\nb\rc".Replace("c", ""), PreviewLanguages.Plain).LineCount);
    }

    [Fact]
    public void Control_characters_are_replaced_not_emitted()
    {
        var html = ToHtml(TextHighlighter.Highlight("a\u0001b\u007f", PreviewLanguages.Plain));

        Assert.False(html.Contains('\u0001'));
        Assert.Contains("\uFFFD", html);
    }

    [Fact]
    public void Very_long_line_is_not_tokenized_past_the_cap()
    {
        var line = string.Concat(Enumerable.Repeat("var a = 1; ", 2000));
        var result = TextHighlighter.Highlight(line, PreviewLanguages.JavaScript);

        AssertRenderable(result, line);
        Assert.True(Regex.Matches(ToHtml(result), "class=\"kw\"").Count < 700);
    }

    [Fact]
    public void Token_budget_stops_spans_but_keeps_all_text()
    {
        var text = string.Concat(Enumerable.Repeat("public class Foo { int x = 1; }\n", 100));

        var result = TextHighlighter.Highlight(text, PreviewLanguages.CSharp, maxTokenSpans: 7);

        Assert.True(result.TokensLimited);
        Assert.Equal(7, result.TokenCount);
        Assert.Equal(result.LineCount + 7, result.DomElementCount);
        AssertRenderable(result, text);
    }

    [Fact]
    public void Token_ranges_use_utf16_code_units()
    {
        var text = "var x = \"\U0001F600\";\n";

        var result = TextHighlighter.Highlight(text, PreviewLanguages.JavaScript);
        var line = Assert.Single(result.Lines);
        var stringToken = Assert.Single(
            line.Tokens,
            token => line.Text.Substring(token.Start, token.Length) == "\"\U0001F600\"");

        Assert.Equal(8, stringToken.Start);
        Assert.Equal(4, stringToken.Length);
    }

    [Fact]
    public void Text_beyond_the_highlight_limit_is_plain()
    {
        var chunk = "var a = 1;\n";
        var text = string.Concat(Enumerable.Repeat(chunk, TextHighlighter.MaxHighlightChars / chunk.Length + 500));
        var result = TextHighlighter.Highlight(text, PreviewLanguages.JavaScript);

        AssertRenderable(result, text);

        var html = ToHtml(result);
        var lastLine = html[html.LastIndexOf("<span class=\"l\">", StringComparison.Ordinal)..];
        Assert.DoesNotContain("class=\"kw\"", lastLine);
    }

    [Fact]
    public void Large_input_is_fast()
    {
        var chunk = "public static async Task<int> Run(string s) { return s.Length + 42; } // c\n";
        var text = string.Concat(Enumerable.Repeat(chunk, 512 * 1024 / chunk.Length));
        var minified = string.Concat(Enumerable.Repeat("a=function(b){return b+1};", 512 * 1024 / 26));

        var watch = System.Diagnostics.Stopwatch.StartNew();
        TextHighlighter.Highlight(text, PreviewLanguages.CSharp);
        var codeMs = watch.ElapsedMilliseconds;

        watch.Restart();
        TextHighlighter.Highlight(minified, PreviewLanguages.JavaScript);
        var minifiedMs = watch.ElapsedMilliseconds;

        Console.WriteLine($"512 KB highlight: code={codeMs} ms, single-line minified={minifiedMs} ms");
        Assert.True(codeMs < 2000 && minifiedMs < 2000);
    }

    [Theory]
    [InlineData("Program.cs", null, "csharp")]
    [InlineData("App.vue", null, "xml")]
    [InlineData("Dockerfile", null, "dockerfile")]
    [InlineData("dockerfile.dev", null, "dockerfile")]
    [InlineData("Containerfile", null, "dockerfile")]
    [InlineData("Containerfile.dev", null, "dockerfile")]
    [InlineData("Makefile", null, "makefile")]
    [InlineData("GNUmakefile", null, "makefile")]
    [InlineData("Makefile.am", null, "makefile")]
    [InlineData("rules.mk", null, "makefile")]
    [InlineData("notes.log", null, "log")]
    [InlineData("paper.tex", null, "tex")]
    [InlineData("app.sln", null, "sln")]
    [InlineData("app.slnx", null, "xml")]
    [InlineData("main.swift", null, "swift")]
    [InlineData("build.scala", null, "scala")]
    [InlineData("index.php", null, "php")]
    [InlineData("Main.kt", null, "kotlin")]
    [InlineData("build.kts", null, "kotlin")]
    [InlineData("app.dart", null, "dart")]
    [InlineData("data.csv", null, "csv")]
    [InlineData("data.tsv", null, "csv")]
    [InlineData("script.ps1", null, "powershell")]
    [InlineData("build.bat", null, "batch")]
    [InlineData("app.rb", null, "ruby")]
    [InlineData("init.lua", null, "lua")]
    [InlineData("analysis.r", null, "r")]
    [InlineData("model.jl", null, "julia")]
    [InlineData("Program.fs", null, "fsharp")]
    [InlineData("Module.vb", null, "vb")]
    [InlineData("styles.scss", null, "scss")]
    [InlineData("styles.sass", null, "sass")]
    [InlineData("styles.less", null, "less")]
    [InlineData("main.tf", null, "hcl")]
    [InlineData("flake.nix", null, "nix")]
    [InlineData("tool.cmake", null, "cmake")]
    [InlineData("CMakeLists.txt", null, "cmake")]
    [InlineData("BUILD", null, "bazel")]
    [InlineData("BUILD.bazel", null, "bazel")]
    [InlineData("WORKSPACE", null, "bazel")]
    [InlineData("MODULE.bazel", null, "bazel")]
    [InlineData("build.gradle", null, "groovy")]
    [InlineData("Jenkinsfile", null, "groovy")]
    [InlineData("Gemfile", null, "ruby")]
    [InlineData("Rakefile", null, "ruby")]
    [InlineData("data.jsonc", null, "json")]
    [InlineData("data.json5", null, "json5")]
    [InlineData("data.jsonl", null, "json")]
    [InlineData("data.ndjson", null, "json")]
    [InlineData("change.diff", null, "diff")]
    [InlineData("change.patch", null, "diff")]
    [InlineData("captions.srt", null, "srt")]
    [InlineData("captions.vtt", null, "vtt")]
    [InlineData("episode.ass", null, "ass")]
    [InlineData("episode.ssa", null, "ass")]
    [InlineData("playlist.m3u", null, "m3u")]
    [InlineData("playlist.m3u8", null, "m3u")]
    [InlineData("album.cue", null, "cue")]
    [InlineData("song.lrc", null, "lrc")]
    [InlineData("release.nfo", null, "nfo")]
    [InlineData(".gitignore", null, "ignore")]
    [InlineData(".gitattributes", null, "ignore")]
    [InlineData(".dockerignore", null, "ignore")]
    [InlineData(".editorconfig", null, "ini")]
    [InlineData(".htaccess", null, "htaccess")]
    [InlineData("nginx.conf", null, "nginx")]
    [InlineData("notes.conf", null, "ini")]
    [InlineData("notes.TXT", null, "plaintext")]
    [InlineData("script", "#!/usr/bin/env python3", "python")]
    [InlineData("run", "#!/bin/bash", "bash")]
    [InlineData("run", "#!/usr/bin/env pwsh", "powershell")]
    [InlineData("run", "#!/usr/bin/env ruby", "ruby")]
    [InlineData("README.md", null, "markdown")]
    [InlineData(".env", null, "ini")]
    [InlineData("data.unknownext", null, "plaintext")]
    [InlineData("build", null, "plaintext")]
    [InlineData("workspace", null, "plaintext")]
    [InlineData("Build", null, "plaintext")]
    [InlineData("BUILD", "#!/bin/sh", "bash")]
    [InlineData("WORKSPACE", "#!/usr/bin/env python3", "python")]
    [InlineData("Gemfile", "#!/usr/bin/env ruby", "ruby")]
    [InlineData("Dockerfile.prod", null, "dockerfile")]
    [InlineData("Dockerfile-dev", null, "dockerfile")]
    [InlineData("Dockerfile.yaml", null, "yaml")]
    [InlineData("Dockerfile.ts", null, "typescript")]
    [InlineData("api.Dockerfile", null, "dockerfile")]
    [InlineData("web.Containerfile", null, "dockerfile")]
    [InlineData("Makefile.json", null, "json")]
    [InlineData("dir\\Dockerfile", null, "dockerfile")]
    [InlineData("src\\CMakeLists.txt", null, "cmake")]
    [InlineData("", null, "plaintext")]
    public void Language_comes_from_the_file_name(string fileName, string? firstLine, string expected)
    {
        Assert.Equal(expected, PreviewLanguages.FromFileName(fileName, firstLine).Name);
    }
}
