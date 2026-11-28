using Alexandria.Common.Helpers;
using AwesomeAssertions;

namespace Alexandria.Tests.Unit.Streaming;

public class TranscodeSupportedFileTypesTests
{
    [Theory]
    [InlineData("video/mp4")]
    [InlineData("video/x-matroska")]
    [InlineData("audio/mpeg")]
    [InlineData("audio/flac")]
    [InlineData("Video/MP4; codecs=avc1")]
    public void IsSupported_audioOrVideo_isTrue(string mimeType)
    {
        TranscodeSupportedFileTypes.IsSupported(mimeType).Should().BeTrue();
    }

    [Theory]
    [InlineData("application/pdf")]
    [InlineData("image/jpeg")]
    [InlineData("text/plain")]
    [InlineData("application/zip")]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void IsSupported_anythingElse_isFalse(string? mimeType)
    {
        TranscodeSupportedFileTypes.IsSupported(mimeType).Should().BeFalse();
    }
}
