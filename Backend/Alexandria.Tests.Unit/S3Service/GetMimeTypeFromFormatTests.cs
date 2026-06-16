using AwesomeAssertions;

namespace Alexandria.Tests.Unit.S3Service;

public class GetMimeTypeFromFormatTests
{
    [Theory]
    [InlineData("webm", "video/webm")]
    [InlineData("matroska", "video/webm")]
    [InlineData("mkv", "video/webm")]
    [InlineData("mp4", "video/mp4")]
    [InlineData("mov", "video/mp4")]
    [InlineData("m4v", "video/mp4")]
    [InlineData("avi", "video/x-msvideo")]
    [InlineData("mpeg", "video/mpeg")]
    [InlineData("mpg", "video/mpeg")]
    [InlineData("mp3", "audio/mpeg")]
    [InlineData("wav", "audio/wav")]
    [InlineData("flac", "audio/flac")]
    [InlineData("ogg", "audio/ogg")]
    public void GetMimeTypeFromFormat_knownFormat(string format, string expected)
    {
        Services.Storage.S3Service.GetMimeTypeFromFormat(format).Should().Be(expected);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("unknown_format")]
    public void GetMimeTypeFromFormat_fallback(string format)
    {
        Services.Storage.S3Service.GetMimeTypeFromFormat(format)
            .Should().Be("application/octet-stream");
    }

    [Theory]
    [InlineData("MP4", "video/mp4")]
    [InlineData("WEBM", "video/webm")]
    [InlineData("MP3", "audio/mpeg")]
    public void GetMimeTypeFromFormat_caseInsensitivity(string format, string expected)
    {
        Services.Storage.S3Service.GetMimeTypeFromFormat(format).Should().Be(expected);
    }
}