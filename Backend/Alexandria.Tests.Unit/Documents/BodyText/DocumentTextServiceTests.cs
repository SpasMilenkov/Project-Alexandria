using System.Text;
using Alexandria.Common.Helpers;
using Alexandria.Data.Models.Enumerators;
using Alexandria.Services.Preview.Documents.BodyText;
using AwesomeAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace Alexandria.Tests.Unit.Documents.BodyText;

public sealed class DocumentTextServiceTests : IDisposable
{
    private const string Docx = "application/vnd.openxmlformats-officedocument.wordprocessingml.document";

    private readonly string _folder = Directory.CreateTempSubdirectory("alexandria-text-tests-").FullName;

    public void Dispose() => Directory.Delete(_folder, recursive: true);

    private static DocumentTextService CreateSut() => new(NullLogger<DocumentTextService>.Instance);

    private string WriteFile(string name, byte[] content)
    {
        var path = Path.Combine(_folder, name);
        File.WriteAllBytes(path, content);

        return path;
    }

    [Fact]
    public async Task Extract_markdown_readsTheTextWithItsSyntax()
    {
        var path = WriteFile("notes.md", Encoding.UTF8.GetBytes("# Paris trip\n\n- hotel *booked*\n"));

        var result = await CreateSut().ExtractAsync("text/markdown", path, TestContext.Current.CancellationToken);

        result.Status.Should().Be(DocumentTextStatus.Extracted);
        result.Text.Should().Be("# Paris trip\n- hotel *booked*");
        result.IsTruncated.Should().BeFalse();
    }

    [Fact]
    public async Task Extract_markdownAlias_resolvesToTheSameReader()
    {
        var path = WriteFile("notes.md", Encoding.UTF8.GetBytes("alias"));

        var result = await CreateSut().ExtractAsync("text/x-markdown", path, TestContext.Current.CancellationToken);

        result.Text.Should().Be("alias");
    }

    [Fact]
    public async Task Extract_plainTextWithByteOrderMark_dropsTheMark()
    {
        var path = WriteFile("notes.txt", [0xEF, 0xBB, 0xBF, .. Encoding.UTF8.GetBytes("café")]);

        var result = await CreateSut().ExtractAsync("text/plain", path, TestContext.Current.CancellationToken);

        result.Text.Should().Be("café");
    }

    [Fact]
    public async Task Extract_blankText_isEmpty()
    {
        var path = WriteFile("blank.txt", Encoding.UTF8.GetBytes(" \n\t \n"));

        var result = await CreateSut().ExtractAsync("text/plain", path, TestContext.Current.CancellationToken);

        result.Status.Should().Be(DocumentTextStatus.Empty);
        result.Text.Should().BeEmpty();
    }

    [Fact]
    public async Task Extract_lockedDocument_returnsNoText()
    {
        var path = WriteFile("locked.docx", [0xD0, 0xCF, 0x11, 0xE0, 0xA1, 0xB1, 0x1A, 0xE1, 0, 0]);

        var result = await CreateSut().ExtractAsync(Docx, path, TestContext.Current.CancellationToken);

        result.Status.Should().Be(DocumentTextStatus.PasswordProtected);
        result.Text.Should().BeEmpty();
        result.IsTruncated.Should().BeFalse();
    }

    [Fact]
    public async Task Extract_corruptPackage_throws()
    {
        var path = WriteFile("broken.docx", Encoding.UTF8.GetBytes("not a zip"));

        var extract = () => CreateSut().ExtractAsync(Docx, path, TestContext.Current.CancellationToken);

        await extract.Should().ThrowAsync<InvalidDataException>();
    }

    [Fact]
    public async Task Extract_unsupportedType_throws()
    {
        var path = WriteFile("song.mp3", [1, 2, 3]);

        var extract = () => CreateSut().ExtractAsync("audio/mpeg", path, TestContext.Current.CancellationToken);

        await extract.Should().ThrowAsync<ArgumentException>();
    }

    [Theory]
    [InlineData(null, 0, DocumentTextStatus.Empty)]
    [InlineData(null, 5, DocumentTextStatus.Extracted)]
    [InlineData(DocumentTextStatus.NoTextLayer, 5, DocumentTextStatus.NoTextLayer)]
    public void Classify_prefersTheReadersVerdict(DocumentTextStatus? readerStatus, int visible,
        DocumentTextStatus expected)
    {
        var collector = new TextCollector();
        collector.Append(new string('x', visible));

        DocumentTextService.Classify(readerStatus, collector).Should().Be(expected);
    }

    [Theory]
    [InlineData("application/pdf", DocumentTextFormat.Pdf)]
    [InlineData("APPLICATION/PDF; charset=binary", DocumentTextFormat.Pdf)]
    [InlineData("application/vnd.oasis.opendocument.presentation", DocumentTextFormat.OpenDocument)]
    [InlineData("application/vnd.oasis.opendocument.spreadsheet", DocumentTextFormat.OpenDocumentSpreadsheet)]
    [InlineData("text/x-markdown", DocumentTextFormat.PlainText)]
    [InlineData("application/x-mobipocket-ebook", null)]
    [InlineData("text/csv", null)]
    [InlineData(null, null)]
    public void FileTypes_resolveTheReaderFormat(string? mimeType, DocumentTextFormat? expected)
    {
        DocumentTextFileTypes.FormatOf(mimeType).Should().Be(expected);
    }
}