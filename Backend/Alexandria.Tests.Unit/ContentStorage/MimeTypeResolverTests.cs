using Alexandria.Common.Mime;
using Alexandria.Services.Storage.Content.Mime;
using Xunit;

namespace Alexandria.Tests.Unit.ContentStorage;

public class MimeTypeResolverTests
{
    private const string Octet = MimeSniffing.Octet;
    private const string Docx = "application/vnd.openxmlformats-officedocument.wordprocessingml.document";
    private const string Xlsx = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

    private sealed class StubSniffer(string? result) : IMimeSniffer
    {
        public string? Detect(byte[] header) => result;
    }

    private sealed class ThrowingSniffer : IMimeSniffer
    {
        public string? Detect(byte[] header) => throw new InvalidOperationException("sniffer must not be called");
    }

    private static byte[] Header(string kind) => kind switch
    {
        "any" => [0x89, 0x01, 0x02, 0x03],
        "txt" => System.Text.Encoding.UTF8.GetBytes("h\u00e9llo w\u00f6rld\n"),
        "bin" => [0x00, 0x01, 0x02, 0xFF],
        "shebang" => System.Text.Encoding.UTF8.GetBytes("#!/bin/sh\necho build\n"),
        "empty" => [],
        "trunc" => Truncated(),
        "badutf8" => [0x41, 0x42, 0xFF, 0xFE, 0x43],
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind)
    };

    /// <summary>16384 bytes of valid UTF-8 cut in the middle of a three byte character.</summary>
    private static byte[] Truncated()
    {
        var bytes = new byte[MimeSniffing.HeaderBytes];
        Array.Fill(bytes, (byte)'a');
        bytes[^2] = 0xE2;
        bytes[^1] = 0x82;
        return bytes;
    }

    public static IEnumerable<object?[]> ResolveCases() =>
    [
        // id, fileName, sniffed, declared, header, expected MIME, expected source
        ["U01", "photo.png", "image/png", null, "any", "image/png", MimeSource.SniffedSpecific],
        ["U02", "photo", "image/jpeg", Octet, "any", "image/jpeg", MimeSource.SniffedSpecific],
        ["U03", "renamed.png", "application/pdf", "image/png", "any", "application/pdf", MimeSource.SniffedSpecific],
        ["U04", "report.docx", "application/zip", Octet, "any", Docx, MimeSource.Extension],
        ["U05", "report", "application/zip", null, "any", "application/zip", MimeSource.SniffedGeneric],
        ["U06", "image.png", "application/zip", "image/png", "any", "application/zip", MimeSource.SniffedGeneric],
        ["U07", "tool.jar", "application/zip", null, "any", "application/java-archive", MimeSource.Extension],
        ["U08", "song.m4a", "video/mp4", null, "any", "audio/mp4", MimeSource.Extension],
        ["U09", "clip.mp4", "audio/mp4", null, "any", "video/mp4", MimeSource.Extension],
        ["U10", "video.webm", "video/x-matroska", null, "any", "video/webm", MimeSource.Extension],
        ["U11", "sheet.xlsx", Docx, null, "any", Xlsx, MimeSource.Extension],
        ["U12", "README.md", null, Octet, "txt", "text/markdown", MimeSource.Extension],
        ["U13", "Program.cs", null, null, "txt", "text/x-csharp", MimeSource.Extension],
        ["U14", "data.json", "text/plain", null, "txt", "application/json", MimeSource.Extension],
        ["U15", "icon.svg", "application/xml", null, "txt", "image/svg+xml", MimeSource.Extension],
        ["U16", "Dockerfile", null, null, "txt", "text/x-dockerfile", MimeSource.FileName],
        ["U17", "LICENSE", null, "text/plain", "txt", "text/plain", MimeSource.Declared],
        ["U18", "blob", null, Octet, "bin", Octet, MimeSource.Fallback],
        ["U19", "empty.bin", null, null, "empty", Octet, MimeSource.Fallback],
        ["U20", "notes.txt", null, null, "empty", "text/plain", MimeSource.Extension],
        ["U21", "a.xyz", null, "application/x-custom", "bin", "application/x-custom", MimeSource.Declared],
        ["U22", "a.xyz", null, "garbage", "bin", Octet, MimeSource.Fallback],
        ["U23", "UPPER.PNG", null, null, "bin", "image/png", MimeSource.Extension],
        ["U24", "a.xyz", null, "text/plain; charset=utf-8", "bin", "text/plain", MimeSource.Declared],
        ["U25", "archive.tar.gz", "application/gzip", null, "bin", "application/gzip", MimeSource.SniffedSpecific],
        ["U26", "stream.ts", "video/mp2t", null, "bin", "video/mp2t", MimeSource.SniffedSpecific],
        ["U27", "app.ts", null, null, "txt", "text/typescript", MimeSource.Extension],
        ["U28", "app.ts", null, null, "bin", Octet, MimeSource.Fallback],
        ["U29", "x.xyz", "text/plain", null, "txt", "text/plain", MimeSource.SniffedGeneric],
        ["U30", "photo.png", "IMAGE/PNG", null, "any", "image/png", MimeSource.SniffedSpecific],
        ["U31", "Dockerfile", null, null, "trunc", "text/x-dockerfile", MimeSource.FileName],
        ["U32", "blob", null, null, "badutf8", Octet, MimeSource.Fallback],
        ["U33", "", null, null, "bin", Octet, MimeSource.Fallback],
        ["U34", "blob", null, "binary/octet-stream", "bin", Octet, MimeSource.Fallback],
        ["U35", "a.wav", "audio/x-wav", null, "any", "audio/wav", MimeSource.SniffedSpecific],
        ["U36", "a", "application/x-zip-compressed", null, "any", "application/zip", MimeSource.SniffedGeneric],
        ["U37", "clip.mov", "video/mp4", null, "any", "video/quicktime", MimeSource.Extension],
        ["U38", "index.htm", null, null, "txt", "text/html", MimeSource.Extension],
        ["U39", "script.js", null, null, "txt", "text/javascript", MimeSource.Extension],
        ["U40", "module.mjs", null, null, "txt", "text/javascript", MimeSource.Extension],
        ["U41", "view.jsx", null, null, "txt", "text/javascript", MimeSource.Extension],
        ["U42", "view.tsx", null, null, "txt", "text/typescript", MimeSource.Extension],
        ["U43", "component.vue", null, null, "txt", "text/x-vue", MimeSource.Extension],
        ["U44", "query.sql", null, null, "txt", "application/sql", MimeSource.Extension],
        ["U45", "run.sh", null, null, "txt", "application/x-sh", MimeSource.Extension],
        ["U46", "App.java", null, null, "txt", "text/x-java-source", MimeSource.Extension],
        ["U47", "main.go", null, null, "txt", "text/x-go", MimeSource.Extension],
        ["U48", "lib.rs", null, null, "txt", "text/x-rust", MimeSource.Extension],
        ["U49", "hash.c", null, null, "txt", "text/x-c", MimeSource.Extension],
        ["U50", "hash.h", null, null, "txt", "text/x-c", MimeSource.Extension],
        ["U51", "hash.cpp", null, null, "txt", "text/x-c++", MimeSource.Extension],
        ["U52", "blob", null, "application/javascript", "bin", "text/javascript", MimeSource.Declared],
        ["U53", "blob", null, "application/typescript", "bin", "text/typescript", MimeSource.Declared],
        ["U54", "README.md", "message/rfc822", null, "txt", "text/markdown", MimeSource.Extension],
        ["U55", "run.sh", "application/x-sh", null, "txt", "application/x-sh", MimeSource.Extension],
        ["U56", "photo.md", "image/png", null, "any", "image/png", MimeSource.SniffedSpecific],
        ["U57", "notes.txt", "application/pdf", null, "bin", "application/pdf", MimeSource.SniffedSpecific],
        ["U58", "mail.eml", "message/rfc822", null, "txt", "message/rfc822", MimeSource.SniffedSpecific],
        ["U59", "Dockerfile", "message/rfc822", null, "txt", "text/x-dockerfile", MimeSource.FileName],
        ["U60", "Dockerfile.dev", "message/rfc822", null, "txt", "text/x-dockerfile", MimeSource.FileName],
        ["U61", "dockerfile", "message/rfc822", null, "txt", "text/x-dockerfile", MimeSource.FileName],
        ["U62", "dir/Dockerfile.prod", "message/rfc822", null, "txt", "text/x-dockerfile", MimeSource.FileName],
        ["U63", "Dockerfile", "message/rfc822", null, "bin", "message/rfc822", MimeSource.SniffedSpecific],
        ["U64", "Dockerfile.ts", "message/rfc822", null, "txt", "text/typescript", MimeSource.Extension],
        ["U65", "Dockerfile.yaml", "message/rfc822", null, "txt", "application/yaml", MimeSource.Extension],
        ["U66", "Containerfile", "message/rfc822", null, "txt", "text/x-containerfile", MimeSource.FileName],
        ["U67", "Containerfile.dev", "message/rfc822", null, "txt", "text/x-containerfile", MimeSource.FileName],
        ["U68", "Makefile", "message/rfc822", null, "txt", "text/x-makefile", MimeSource.FileName],
        ["U69", "GNUmakefile", "message/rfc822", null, "txt", "text/x-makefile", MimeSource.FileName],
        ["U70", "Makefile.am", "message/rfc822", null, "txt", "text/x-makefile", MimeSource.FileName],
        ["U71", "notes.log", "text/plain", null, "txt", "text/x-log", MimeSource.Extension],
        ["U72", "paper.tex", null, null, "txt", "text/x-tex", MimeSource.Extension],
        ["U73", "main.swift", null, null, "txt", "text/x-swift", MimeSource.Extension],
        ["U74", "build.scala", null, null, "txt", "text/x-scala", MimeSource.Extension],
        ["U75", "Main.kt", null, null, "txt", "text/x-kotlin", MimeSource.Extension],
        ["U76", "build.kts", null, null, "txt", "text/x-kotlin-script", MimeSource.Extension],
        ["U77", "app.dart", null, null, "txt", "application/dart", MimeSource.Extension],
        ["U78", "index.php", null, null, "txt", "application/x-php", MimeSource.Extension],
        ["U79", "app.sln", null, null, "txt", "text/x-sln", MimeSource.Extension],
        ["U80", "app.slnx", null, null, "txt", "text/x-slnx", MimeSource.Extension],
        ["U81", "Makefile", "message/rfc822", null, "bin", "message/rfc822", MimeSource.SniffedSpecific],
        ["U82", "Containerfile", "message/rfc822", null, "bin", "message/rfc822", MimeSource.SniffedSpecific],
        ["U83", "rules.mk", null, null, "txt", "text/x-makefile", MimeSource.Extension],
        ["U84", "paper.tex", "application/x-tex", null, "txt", "text/x-tex", MimeSource.Extension],
        ["U85", "script.ps1", null, null, "txt", "text/x-powershell", MimeSource.Extension],
        ["U86", "build.bat", null, null, "txt", "text/x-batch", MimeSource.Extension],
        ["U87", "app.rb", null, null, "txt", "text/x-ruby", MimeSource.Extension],
        ["U88", "init.lua", null, null, "txt", "text/x-lua", MimeSource.Extension],
        ["U89", "analysis.r", null, null, "txt", "text/x-r", MimeSource.Extension],
        ["U90", "model.jl", null, null, "txt", "text/x-julia", MimeSource.Extension],
        ["U91", "Program.fs", null, null, "txt", "text/x-fsharp", MimeSource.Extension],
        ["U92", "Module.vb", null, null, "txt", "text/x-vb", MimeSource.Extension],
        ["U93", "styles.scss", null, null, "txt", "text/x-scss", MimeSource.Extension],
        ["U94", "styles.sass", null, null, "txt", "text/x-sass", MimeSource.Extension],
        ["U95", "styles.less", null, null, "txt", "text/x-less", MimeSource.Extension],
        ["U96", "main.tf", null, null, "txt", "text/x-hcl", MimeSource.Extension],
        ["U97", "flake.nix", null, null, "txt", "text/x-nix", MimeSource.Extension],
        ["U98", "tool.cmake", null, null, "txt", "text/x-cmake", MimeSource.Extension],
        ["U99", "CMakeLists.txt", null, null, "txt", "text/x-cmake", MimeSource.FileName],
        ["U100", "BUILD", null, null, "txt", "text/x-bazel", MimeSource.FileName],
        ["U101", "BUILD.bazel", null, null, "txt", "text/x-bazel", MimeSource.Extension],
        ["U102", "WORKSPACE", null, null, "txt", "text/x-bazel", MimeSource.FileName],
        ["U103", "MODULE.bazel", null, null, "txt", "text/x-bazel", MimeSource.Extension],
        ["U104", "build.gradle", null, null, "txt", "text/x-gradle", MimeSource.Extension],
        ["U105", "Jenkinsfile", null, null, "txt", "text/x-groovy", MimeSource.FileName],
        ["U106", "app.csproj", null, null, "txt", "application/xml", MimeSource.Extension],
        ["U107", "app.fsproj", null, null, "txt", "application/xml", MimeSource.Extension],
        ["U108", "app.props", null, null, "txt", "application/xml", MimeSource.Extension],
        ["U109", "data.jsonc", null, null, "txt", "application/jsonc", MimeSource.Extension],
        ["U110", "data.json5", null, null, "txt", "application/json5", MimeSource.Extension],
        ["U111", "events.jsonl", null, null, "txt", "application/x-ndjson", MimeSource.Extension],
        ["U112", "events.ndjson", null, null, "txt", "application/x-ndjson", MimeSource.Extension],
        ["U113", "change.diff", null, null, "txt", "text/x-diff", MimeSource.Extension],
        ["U114", "change.patch", null, null, "txt", "text/x-diff", MimeSource.Extension],
        ["U115", "Gemfile", null, null, "txt", "text/x-ruby", MimeSource.FileName],
        ["U116", "Rakefile", null, null, "txt", "text/x-ruby", MimeSource.FileName],
        ["U117", "CMakeLists.txt", "message/rfc822", null, "txt", "text/x-cmake", MimeSource.FileName],
        ["U118", "CMakeLists.txt", null, null, "bin", "text/plain", MimeSource.Extension],
        ["U119", "captions.srt", null, null, "txt", "text/x-srt", MimeSource.Extension],
        ["U120", "captions.vtt", null, null, "txt", "text/x-vtt", MimeSource.Extension],
        ["U121", "episode.ass", null, null, "txt", "text/x-ass", MimeSource.Extension],
        ["U122", "episode.ssa", null, null, "txt", "text/x-ass", MimeSource.Extension],
        ["U123", "playlist.m3u", null, null, "txt", "text/x-m3u", MimeSource.Extension],
        ["U124", "playlist.m3u8", null, null, "txt", "text/x-m3u8", MimeSource.Extension],
        ["U125", "album.cue", null, null, "txt", "text/x-cue", MimeSource.Extension],
        ["U126", "song.lrc", null, null, "txt", "text/x-lrc", MimeSource.Extension],
        ["U127", "release.nfo", null, null, "txt", "text/x-nfo", MimeSource.Extension],
        ["U128", ".gitignore", null, null, "txt", "text/x-gitignore", MimeSource.Extension],
        ["U129", ".gitattributes", null, null, "txt", "text/x-gitattributes", MimeSource.Extension],
        ["U130", ".dockerignore", null, null, "txt", "text/x-dockerignore", MimeSource.Extension],
        ["U131", ".editorconfig", null, null, "txt", "text/x-editorconfig", MimeSource.Extension],
        ["U132", ".htaccess", null, null, "txt", "text/x-htaccess", MimeSource.Extension],
        ["U133", "nginx.conf", null, null, "txt", "text/x-nginx", MimeSource.FileName],
        ["U134", "nginx.conf", "message/rfc822", null, "txt", "text/x-nginx", MimeSource.FileName],
        ["U135", "notes.conf", null, null, "txt", "text/plain", MimeSource.Extension],
        ["U136", "nginx.conf", null, null, "bin", "text/plain", MimeSource.Extension],
        ["U137", "photo.avif", null, null, "bin", "image/avif", MimeSource.Extension],
        ["U138", "photo.avif", "image/avif", null, "any", "image/avif", MimeSource.SniffedSpecific],
        ["U139", "query.sql", "message/rfc822", null, "txt", "application/sql", MimeSource.Extension],
        ["U140", "index.php", "message/rfc822", null, "txt", "application/x-php", MimeSource.Extension],
        ["U141", "events.ndjson", "message/rfc822", null, "txt", "application/x-ndjson", MimeSource.Extension],
        ["U142", "query.sql", "image/png", null, "bin", "image/png", MimeSource.SniffedSpecific],
        ["U143", "compose.yml", null, null, "txt", "application/yaml", MimeSource.Extension],
        ["U144", "config.yaml", null, "text/yaml", "txt", "application/yaml", MimeSource.Extension],
        ["U145", "Cargo.toml", "message/rfc822", null, "txt", "application/toml", MimeSource.Extension],
        ["U146", "settings.ini", null, null, "txt", "text/plain", MimeSource.Extension],
        ["U147", "build", null, null, "txt", "text/plain", MimeSource.TextHeuristic],
        ["U148", "workspace", null, null, "txt", "text/plain", MimeSource.TextHeuristic],
        ["U149", "Build", null, null, "txt", "text/plain", MimeSource.TextHeuristic],
        ["U150", "BUILD", null, null, "shebang", "text/plain", MimeSource.TextHeuristic],
        ["U151", "WORKSPACE", null, null, "shebang", "text/plain", MimeSource.TextHeuristic],
        ["U152", "Gemfile", null, null, "shebang", "text/x-ruby", MimeSource.FileName],
        ["U153", "Dockerfile", null, null, "shebang", "text/x-dockerfile", MimeSource.FileName],
        ["U154", "api.Dockerfile", null, null, "txt", "text/x-dockerfile", MimeSource.Extension],
        ["U155", "prod.dockerfile", "message/rfc822", null, "txt", "text/x-dockerfile", MimeSource.Extension],
        ["U156", "web.Containerfile", null, null, "txt", "text/x-containerfile", MimeSource.Extension],
        ["U157", "api.Dockerfile", "message/rfc822", null, "bin", "message/rfc822", MimeSource.SniffedSpecific],
        ["U158", "dir\\Dockerfile", null, null, "txt", "text/x-dockerfile", MimeSource.FileName],
        ["U159", "src\\CMakeLists.txt", null, null, "txt", "text/x-cmake", MimeSource.FileName],
    ];

    [Theory]
    [MemberData(nameof(ResolveCases))]
    public void Resolve_follows_the_spec_table(
        string id, string fileName, string? sniffed, string? declared, string headerKind,
        string expectedMime, MimeSource expectedSource)
    {
        var resolver = new MimeTypeResolver(new StubSniffer(sniffed));

        var result = resolver.Resolve(fileName, Header(headerKind), declared);

        Assert.Equal(expectedMime, result.MimeType);
        Assert.Equal(expectedSource, result.Source);
    }

    public static IEnumerable<object?[]> WithoutContentCases() =>
    [
        // id, fileName, declared, expected MIME, expected source
        ["E01", "secret.pdf", Octet, "application/pdf", MimeSource.Extension],
        ["E02", "x.xyz", Octet, Octet, MimeSource.Fallback],
        ["E03", "a.bin", "image/png", "image/png", MimeSource.Declared],
        ["E04", "app.ts", Octet, Octet, MimeSource.Fallback],
        ["E05", "song.mp4", Octet, "video/mp4", MimeSource.Extension],
        ["E06", "Dockerfile", Octet, "text/x-dockerfile", MimeSource.FileName],
        ["E07", "Dockerfile.prod", Octet, "text/x-dockerfile", MimeSource.FileName],
        ["E08", "Dockerfile.yaml", Octet, "application/yaml", MimeSource.Extension],
        ["E09", "Containerfile", Octet, "text/x-containerfile", MimeSource.FileName],
        ["E10", "Makefile", Octet, "text/x-makefile", MimeSource.FileName],
        ["E11", "GNUmakefile", Octet, "text/x-makefile", MimeSource.FileName],
        ["E12", "notes.log", Octet, "text/x-log", MimeSource.Extension],
        ["E13", "paper.tex", Octet, "text/x-tex", MimeSource.Extension],
        ["E14", "CMakeLists.txt", Octet, "text/x-cmake", MimeSource.FileName],
        ["E15", "BUILD", Octet, "text/x-bazel", MimeSource.FileName],
        ["E16", "Gemfile", Octet, "text/x-ruby", MimeSource.FileName],
        ["E17", "app.csproj", Octet, "application/xml", MimeSource.Extension],
        ["E18", "data.json5", Octet, "application/json5", MimeSource.Extension],
        ["E19", "change.diff", Octet, "text/x-diff", MimeSource.Extension],
        ["E20", "Jenkinsfile", Octet, "text/x-groovy", MimeSource.FileName],
        ["E21", "captions.srt", Octet, "text/x-srt", MimeSource.Extension],
        ["E22", "playlist.m3u8", Octet, "text/x-m3u8", MimeSource.Extension],
        ["E23", "album.cue", Octet, "text/x-cue", MimeSource.Extension],
        ["E24", "song.lrc", Octet, "text/x-lrc", MimeSource.Extension],
        ["E25", "release.nfo", Octet, "text/x-nfo", MimeSource.Extension],
        ["E26", ".gitignore", Octet, "text/x-gitignore", MimeSource.Extension],
        ["E27", ".htaccess", Octet, "text/x-htaccess", MimeSource.Extension],
        ["E28", "nginx.conf", Octet, "text/x-nginx", MimeSource.FileName],
        ["E29", "notes.conf", Octet, "text/plain", MimeSource.Extension],
        ["E30", "photo.avif", Octet, "image/avif", MimeSource.Extension],
        ["E31", "build", Octet, Octet, MimeSource.Fallback],
        ["E32", "WORKSPACE", Octet, "text/x-bazel", MimeSource.FileName],
        ["E33", "api.Dockerfile", Octet, "text/x-dockerfile", MimeSource.Extension],
        ["E34", "dir\\Containerfile", Octet, "text/x-containerfile", MimeSource.FileName],
    ];

    [Theory]
    [MemberData(nameof(WithoutContentCases))]
    public void ResolveWithoutContent_follows_the_spec_table(
        string id, string fileName, string? declared, string expectedMime, MimeSource expectedSource)
    {
        var resolver = new MimeTypeResolver(new ThrowingSniffer());

        var result = resolver.ResolveWithoutContent(fileName, declared);

        Assert.Equal(expectedMime, result.MimeType);
        Assert.Equal(expectedSource, result.Source);
    }

    [Fact]
    public void Resolve_with_empty_header_does_not_call_the_sniffer()
    {
        var resolver = new MimeTypeResolver(new ThrowingSniffer());

        var result = resolver.Resolve("notes.txt", [], null);

        Assert.Equal("text/plain", result.MimeType);
    }

    [Fact]
    public void Resolve_never_throws_and_always_returns_a_canonical_type()
    {
        string?[] sniffedPool =
        [
            null, "", "garbage", "image/png", "IMAGE/PNG", "audio/x-wav", "application/x-zip-compressed",
            "text/xml", "text/plain; charset=utf-8", "video/mp4", "application/octet-stream", "a/b/c", "x/"
        ];
        string?[] namePool =
        [
            null, "", "a", ".", "a.", ".hidden", "a.PNG", "x.ts", "dir/file.md", "weird\0name.cs",
            "archive.tar.gz", "song.m4a", "report.docx", "no_extension"
        ];
        string?[] declaredPool = [null, "", "garbage", Octet, "image/png", "text/plain; charset=utf-8", "a/b/c"];

        var random = new Random(12345);

        for (var i = 0; i < 3000; i++)
        {
            var length = random.Next(4) == 0 ? MimeSniffing.HeaderBytes : random.Next(0, 64);
            var header = new byte[length];
            random.NextBytes(header);
            if (random.Next(3) == 0) Array.Fill(header, (byte)'x');

            var sniffed = sniffedPool[random.Next(sniffedPool.Length)];
            var name = namePool[random.Next(namePool.Length)]!;
            var declared = declaredPool[random.Next(declaredPool.Length)];

            var resolver = new MimeTypeResolver(new StubSniffer(sniffed));
            var first = resolver.Resolve(name, header, declared);
            var second = resolver.Resolve(name, header, declared);

            Assert.Equal(first, second);
            Assert.True(!string.IsNullOrEmpty(first.MimeType) && first.MimeType.Contains('/'));
            Assert.Equal(first.MimeType, MimeAliases.Canonicalize(first.MimeType));

            var without = resolver.ResolveWithoutContent(name, declared);
            Assert.True(!string.IsNullOrEmpty(without.MimeType) && without.MimeType.Contains('/'));
            Assert.Equal(without.MimeType, MimeAliases.Canonicalize(without.MimeType));
        }
    }

    [Fact]
    public void A01_alias_table_is_self_consistent()
    {
        foreach (var (alias, canonical) in MimeAliases.Table)
        {
            Assert.Equal(canonical, MimeAliases.Canonicalize(alias));
            Assert.Equal(canonical, MimeAliases.Canonicalize(canonical));
            Assert.False(MimeAliases.Table.ContainsKey(canonical));
        }
    }

    [Fact]
    public void A01_every_type_the_resolver_can_emit_is_canonical()
    {
        foreach (var mime in ExtensionMimeMap.AllTypes
                     .Concat(MimeFamilies.AllTypes)
                     .Concat(MimeFileNameMap.AllTypes)
                     .Concat(IsoBmffBrands.AllTypes))
        {
            Assert.Equal(mime, MimeAliases.Canonicalize(mime));
            Assert.True(MimeAliases.HasValidShape(mime));
        }
    }

    [Fact]
    public void Canonicalize_is_idempotent_and_strips_parameters()
    {
        string?[] samples = [null, "", "  Text/Plain ; charset=UTF-8", "audio/x-wav", "binary/octet-stream", "AUDIO/MP3"];

        foreach (var sample in samples)
        {
            var once = MimeAliases.Canonicalize(sample);
            Assert.Equal(once, MimeAliases.Canonicalize(once));
        }

        Assert.Equal("text/plain", MimeAliases.Canonicalize("  Text/Plain ; charset=UTF-8"));
        Assert.Equal("audio/mpeg", MimeAliases.Canonicalize("AUDIO/MP3"));
    }
}
