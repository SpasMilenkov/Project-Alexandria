using Alexandria.Common;
using Alexandria.Common.Audit;
using Alexandria.Common.Config;
using Alexandria.Common.Queues;
using Alexandria.Common.Services;
using Alexandria.Data.Models.Enumerators;
using Alexandria.Services.Storage.Content;
using Alexandria.Services.Storage.Content.Mime;
using Amazon.S3;
using AwesomeAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace Alexandria.Tests.Unit.ContentStorage;

public class CategorizeFileTests
{
    private static ContentStorageService CreateSut()
    {
        return new ContentStorageService(
            Substitute.For<IAmazonS3>(),
            Substitute.For<IAmazonS3>(),
            Substitute.For<IUnitOfWork>(),
            Substitute.For<IOptions<S3Config>>(),
            Substitute.For<ILogger<ContentStorageService>>(),
            Substitute.For<IPromotionQueue>(),
            Substitute.For<IFileService>(),
            new AuditContext(),
            Substitute.For<IUserSettingsService>(),
            Substitute.For<IPublisherService>(),
            new MimeTypeResolver(Substitute.For<IMimeSniffer>()));
    }

    public static IEnumerable<object[]> ImageCases()
    {
        yield return ["image/jpeg", FileCategory.Image];
        yield return ["image/jpg", FileCategory.Image];
        yield return ["image/png", FileCategory.Image];
        yield return ["image/gif", FileCategory.Image];
        yield return ["image/webp", FileCategory.Image];
        yield return ["image/bmp", FileCategory.Image];
        yield return ["image/tiff", FileCategory.Image];
        yield return ["image/svg+xml", FileCategory.Image];
    }

    public static IEnumerable<object[]> AudioCases()
    {
        yield return ["audio/mpeg", FileCategory.Audio];
        yield return ["audio/mp3", FileCategory.Audio];
        yield return ["audio/wav", FileCategory.Audio];
        yield return ["audio/ogg", FileCategory.Audio];
        yield return ["audio/flac", FileCategory.Audio];
        yield return ["audio/aac", FileCategory.Audio];
    }

    public static IEnumerable<object[]> VideoCases()
    {
        yield return ["video/mp4", FileCategory.Video];
        yield return ["video/x-msvideo", FileCategory.Video];
        yield return ["video/x-matroska", FileCategory.Video];
        yield return ["video/webm", FileCategory.Video];
        yield return ["video/quicktime", FileCategory.Video];
    }

    public static IEnumerable<object[]> DocumentCases()
    {
        yield return ["application/msword", FileCategory.Document];
        yield return ["application/vnd.openxmlformats-officedocument.wordprocessingml.document", FileCategory.Document];
        yield return ["application/vnd.oasis.opendocument.text", FileCategory.Document];
        yield return ["application/rtf", FileCategory.Document];
    }

    public static IEnumerable<object[]> SpreadsheetCases()
    {
        yield return ["application/vnd.ms-excel", FileCategory.Spreadsheet];
        yield return ["application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", FileCategory.Spreadsheet];
        yield return ["application/vnd.oasis.opendocument.spreadsheet", FileCategory.Spreadsheet];
    }

    public static IEnumerable<object[]> PresentationCases()
    {
        yield return ["application/vnd.ms-powerpoint", FileCategory.Presentation];
        yield return
            ["application/vnd.openxmlformats-officedocument.presentationml.presentation", FileCategory.Presentation];
        yield return ["application/vnd.oasis.opendocument.presentation", FileCategory.Presentation];
    }

    public static IEnumerable<object[]> PdfCases()
    {
        yield return ["application/pdf", FileCategory.Pdf];
    }

    public static IEnumerable<object[]> ArchiveCases()
    {
        yield return ["application/zip", FileCategory.Archive];
        yield return ["application/x-7z-compressed", FileCategory.Archive];
        yield return ["application/x-rar-compressed", FileCategory.Archive];
        yield return ["application/gzip", FileCategory.Archive];
        yield return ["application/x-tar", FileCategory.Archive];
        yield return ["application/x-xz", FileCategory.Archive];
    }

    public static IEnumerable<object[]> TextCases()
    {
        yield return ["text/markdown", FileCategory.Text];
        yield return ["application/json", FileCategory.Text];
        yield return ["application/xml", FileCategory.Text];
        yield return ["text/xml", FileCategory.Text];
        yield return ["text/html", FileCategory.Text];
        yield return ["text/plain", FileCategory.Text];
        yield return ["application/sql", FileCategory.Text];
        yield return ["application/x-sh", FileCategory.Text];
        yield return ["application/x-ndjson", FileCategory.Text];
    }

    public static IEnumerable<object[]> EdgeCases()
    {
        yield return [null!, FileCategory.Unknown];
        yield return ["", FileCategory.Unknown];
        yield return ["   ", FileCategory.Unknown];
        yield return ["application/octet-stream", FileCategory.Unknown];
        yield return ["application/epub+zip", FileCategory.Unknown];
    }

    public static IEnumerable<object[]> CaseInsensitivityCases()
    {
        yield return ["image/JPEG", FileCategory.Image];
        yield return ["IMAGE/jpeg", FileCategory.Image];
        yield return ["Audio/MP3", FileCategory.Audio];
        yield return ["VIDEO/MP4", FileCategory.Video];
        yield return ["Application/PDF", FileCategory.Pdf];
        yield return ["TEXT/PLAIN", FileCategory.Text];
    }

    [Theory, MemberData(nameof(ImageCases))]
    public void CategorizeFile_images(string mimeType, FileCategory expected)
    {
        CreateSut().CategorizeFile(mimeType).Should().Be(expected);
    }

    [Theory, MemberData(nameof(AudioCases))]
    public void CategorizeFile_audio(string mimeType, FileCategory expected)
    {
        CreateSut().CategorizeFile(mimeType).Should().Be(expected);
    }

    [Theory, MemberData(nameof(VideoCases))]
    public void CategorizeFile_video(string mimeType, FileCategory expected)
    {
        CreateSut().CategorizeFile(mimeType).Should().Be(expected);
    }

    [Theory, MemberData(nameof(DocumentCases))]
    public void CategorizeFile_document(string mimeType, FileCategory expected)
    {
        CreateSut().CategorizeFile(mimeType).Should().Be(expected);
    }

    [Theory, MemberData(nameof(SpreadsheetCases))]
    public void CategorizeFile_spreadsheet(string mimeType, FileCategory expected)
    {
        CreateSut().CategorizeFile(mimeType).Should().Be(expected);
    }

    [Theory, MemberData(nameof(PresentationCases))]
    public void CategorizeFile_presentation(string mimeType, FileCategory expected)
    {
        CreateSut().CategorizeFile(mimeType).Should().Be(expected);
    }

    [Theory, MemberData(nameof(PdfCases))]
    public void CategorizeFile_pdf(string mimeType, FileCategory expected)
    {
        CreateSut().CategorizeFile(mimeType).Should().Be(expected);
    }

    [Theory, MemberData(nameof(ArchiveCases))]
    public void CategorizeFile_archive(string mimeType, FileCategory expected)
    {
        CreateSut().CategorizeFile(mimeType).Should().Be(expected);
    }

    [Theory, MemberData(nameof(TextCases))]
    public void CategorizeFile_text(string mimeType, FileCategory expected)
    {
        CreateSut().CategorizeFile(mimeType).Should().Be(expected);
    }

    [Theory, MemberData(nameof(EdgeCases))]
    public void CategorizeFile_edgeCases(string mimeType, FileCategory expected)
    {
        CreateSut().CategorizeFile(mimeType).Should().Be(expected);
    }

    [Theory, MemberData(nameof(CaseInsensitivityCases))]
    public void CategorizeFile_caseInsensitivity(string mimeType, FileCategory expected)
    {
        CreateSut().CategorizeFile(mimeType).Should().Be(expected);
    }

    public static IEnumerable<object[]> NewTypeCases()
    {
        yield return ["audio/mp4", FileCategory.Audio];
        yield return ["audio/x-m4a", FileCategory.Audio];
        yield return ["audio/opus", FileCategory.Audio];
        yield return ["audio/webm", FileCategory.Audio];
        yield return ["audio/x-wav", FileCategory.Audio];
        yield return ["video/x-m4v", FileCategory.Video];
        yield return ["video/ogg", FileCategory.Video];
        yield return ["video/mp2t", FileCategory.Video];
        yield return ["application/java-archive", FileCategory.Archive];
        yield return ["application/vnd.android.package-archive", FileCategory.Archive];
        yield return ["application/x-zip-compressed", FileCategory.Archive];
        yield return ["text/css", FileCategory.Text];
        yield return ["text/csv", FileCategory.Text];
        yield return ["text/tab-separated-values", FileCategory.Text];
        yield return ["text/xml", FileCategory.Text];
        yield return ["text/x-csharp", FileCategory.Text];
        yield return ["text/x-vue", FileCategory.Text];
        yield return ["text/x-dockerfile", FileCategory.Text];
        yield return ["text/x-containerfile", FileCategory.Text];
        yield return ["text/x-makefile", FileCategory.Text];
        yield return ["text/x-log", FileCategory.Text];
        yield return ["text/x-tex", FileCategory.Text];
        yield return ["application/x-tex", FileCategory.Text];
        yield return ["text/x-slnx", FileCategory.Text];
        yield return ["text/x-kotlin-script", FileCategory.Text];
        yield return ["application/dart", FileCategory.Text];
        yield return ["text/x-dart", FileCategory.Text];
        yield return ["application/x-php", FileCategory.Text];
        yield return ["text/x-php", FileCategory.Text];
        yield return ["application/x-httpd-php", FileCategory.Text];
        yield return ["text/typescript", FileCategory.Text];
        yield return ["application/sql", FileCategory.Text];
        yield return ["application/x-sh", FileCategory.Text];
        yield return ["application/javascript", FileCategory.Text];
        yield return ["text/x-powershell", FileCategory.Text];
        yield return ["text/x-batch", FileCategory.Text];
        yield return ["text/x-ruby", FileCategory.Text];
        yield return ["text/x-lua", FileCategory.Text];
        yield return ["text/x-r", FileCategory.Text];
        yield return ["text/x-julia", FileCategory.Text];
        yield return ["text/x-fsharp", FileCategory.Text];
        yield return ["text/x-vb", FileCategory.Text];
        yield return ["text/x-scss", FileCategory.Text];
        yield return ["text/x-sass", FileCategory.Text];
        yield return ["text/x-less", FileCategory.Text];
        yield return ["text/x-hcl", FileCategory.Text];
        yield return ["text/x-nix", FileCategory.Text];
        yield return ["text/x-cmake", FileCategory.Text];
        yield return ["text/x-bazel", FileCategory.Text];
        yield return ["text/x-gradle", FileCategory.Text];
        yield return ["text/x-groovy", FileCategory.Text];
        yield return ["application/jsonc", FileCategory.Text];
        yield return ["application/json5", FileCategory.Text];
        yield return ["application/x-ndjson", FileCategory.Text];
        yield return ["text/x-diff", FileCategory.Text];
        yield return ["text/x-srt", FileCategory.Text];
        yield return ["text/x-vtt", FileCategory.Text];
        yield return ["text/x-ass", FileCategory.Text];
        yield return ["text/x-m3u", FileCategory.Text];
        yield return ["text/x-m3u8", FileCategory.Text];
        yield return ["text/x-cue", FileCategory.Text];
        yield return ["text/x-lrc", FileCategory.Text];
        yield return ["text/x-nfo", FileCategory.Text];
        yield return ["text/x-gitignore", FileCategory.Text];
        yield return ["text/x-gitattributes", FileCategory.Text];
        yield return ["text/x-dockerignore", FileCategory.Text];
        yield return ["text/x-editorconfig", FileCategory.Text];
        yield return ["text/x-htaccess", FileCategory.Text];
        yield return ["text/x-nginx", FileCategory.Text];
        yield return ["image/heic", FileCategory.Image];
        yield return ["IMAGE/HEIC", FileCategory.Image];
        yield return ["application/ogg", FileCategory.Unknown];
    }

    [Theory, MemberData(nameof(NewTypeCases))]
    public void CategorizeFile_newTypes(string mimeType, FileCategory expected)
    {
        CreateSut().CategorizeFile(mimeType).Should().Be(expected);
    }
}
