using Alexandria.Common.Helpers;
using AwesomeAssertions;

namespace Alexandria.Tests.Unit.AutoTagging;

public class AutoTagSupportedFileTypesTests
{
    [Theory]
    [InlineData("audio/mpeg", true)]
    [InlineData("audio/ogg", true)]
    [InlineData("audio/flac", true)]
    [InlineData("application/ogg", true)]
    [InlineData("AUDIO/MPEG", true)]
    [InlineData(" application/ogg ", true)]
    [InlineData("video/mp4", false)]
    [InlineData("application/pdf", false)]
    [InlineData("", false)]
    [InlineData("   ", false)]
    [InlineData(null, false)]
    public void IsSupported(string? mimeType, bool expected)
    {
        AutoTagSupportedFileTypes.IsSupported(mimeType).Should().Be(expected);
    }
}