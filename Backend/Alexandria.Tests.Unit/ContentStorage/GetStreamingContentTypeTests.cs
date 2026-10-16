using Alexandria.Services.Storage.Content;
using AwesomeAssertions;

namespace Alexandria.Tests.Unit.ContentStorage;

public class GetStreamingContentTypeTests
{
    [Theory]
    [InlineData(".m3u8", "application/x-mpegURL")]
    [InlineData(".mpd", "application/dash+xml")]
    [InlineData(".ts", "video/mp2t")]
    [InlineData(".m4s", "video/iso.segment")]
    [InlineData(".mp4", "video/mp4")]
    [InlineData(".m4a", "audio/mp4")]
    public void GetStreamingContentType_knownExtensions(string extension, string expected)
    {
        ContentStorageService.GetStreamingContentType(extension).Should().Be(expected);
    }

    [Theory]
    [InlineData(".txt")]
    [InlineData(".pdf")]
    [InlineData(".zip")]
    [InlineData("")]
    public void GetStreamingContentType_unknownExtension(string extension)
    {
        ContentStorageService.GetStreamingContentType(extension)
            .Should().Be("application/octet-stream");
    }

    [Theory]
    [InlineData(".M3U8", "application/x-mpegURL")]
    [InlineData(".MPD", "application/dash+xml")]
    [InlineData(".MP4", "video/mp4")]
    public void GetStreamingContentType_caseInsensitivity(string extension, string expected)
    {
        ContentStorageService.GetStreamingContentType(extension).Should().Be(expected);
    }
}