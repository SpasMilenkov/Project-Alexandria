using Alexandria.Common;
using Alexandria.Common.Audit;
using Alexandria.Common.Config;
using Alexandria.Common.Queues;
using Alexandria.Common.Services;
using Alexandria.Data.Models.Enumerators;
using Amazon.S3;
using AwesomeAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace Alexandria.Tests.Unit.S3Service;

public class CategorizeFileTests
{
    private static Services.Storage.S3Service CreateSut()
    {
        return new Services.Storage.S3Service(
            Substitute.For<IAmazonS3>(),
            Substitute.For<IAmazonS3>(),
            Substitute.For<IUnitOfWork>(),
            Substitute.For<IOptions<S3Config>>(),
            Substitute.For<ILogger<Services.Storage.S3Service>>(),
            Substitute.For<IPromotionQueue>(),
            Substitute.For<IFileService>(),
            new AuditContext());
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
    }

    public static IEnumerable<object[]> EdgeCases()
    {
        yield return [null!, FileCategory.Unknown];
        yield return ["", FileCategory.Unknown];
        yield return ["   ", FileCategory.Unknown];
        yield return ["application/octet-stream", FileCategory.Unknown];
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
}