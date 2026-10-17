using System.IO.Compression;
using Alexandria.Services.Storage.Content;
using AwesomeAssertions;

namespace Alexandria.Tests.Unit.ContentStorage;

public class GetCompressionLevelTests
{
    [Theory]
    [InlineData("photo.jpg")]
    [InlineData("photo.jpeg")]
    [InlineData("photo.png")]
    [InlineData("photo.webp")]
    [InlineData("photo.gif")]
    [InlineData("video.mp4")]
    [InlineData("video.mkv")]
    [InlineData("video.webm")]
    [InlineData("video.mov")]
    [InlineData("audio.mp3")]
    [InlineData("audio.flac")]
    [InlineData("audio.aac")]
    [InlineData("audio.ogg")]
    [InlineData("archive.zip")]
    [InlineData("archive.7z")]
    [InlineData("archive.gz")]
    [InlineData("archive.rar")]
    public void GetCompressionLevel_compressedFormats(string fileName)
    {
        ContentStorageService.GetCompressionLevel(fileName)
            .Should().Be(CompressionLevel.NoCompression);
    }

    [Theory]
    [InlineData("document.pdf")]
    [InlineData("text.txt")]
    [InlineData("report.docx")]
    [InlineData("spreadsheet.xlsx")]
    [InlineData("")]
    [InlineData("noextension")]
    public void GetCompressionLevel_uncompressedFormats(string fileName)
    {
        ContentStorageService.GetCompressionLevel(fileName)
            .Should().Be(CompressionLevel.Fastest);
    }

    [Theory]
    [InlineData("photo.JPG")]
    [InlineData("VIDEO.MP4")]
    [InlineData("Archive.ZIP")]
    public void GetCompressionLevel_caseInsensitivity(string fileName)
    {
        ContentStorageService.GetCompressionLevel(fileName)
            .Should().Be(CompressionLevel.NoCompression);
    }
}