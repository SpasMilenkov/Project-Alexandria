using System.Text.Json;
using Alexandria.Services.Preview.Media;
using Alexandria.Services.Preview.Media.Dto;
using AwesomeAssertions;

namespace Alexandria.Tests.Unit.PreviewJobs;

public class MediaMetadataExtractionTests
{
    [Theory]
    [InlineData("title", "artist", "album", "date", "genre")]
    [InlineData("TITLE", "ARTIST", "ALBUM", "DATE", "GENRE")]
    [InlineData("Title", "aRtIsT", "Album", "Date", "Genre")]
    public void MapMetadata_flacTagCasing_extractsDescriptiveFields(
        string titleKey,
        string artistKey,
        string albumKey,
        string dateKey,
        string genreKey)
    {
        var output = $$"""
            {
                "streams": [{ "codec_name": "flac", "codec_type": "audio" }],
                "format": {
                    "format_name": "flac",
                    "duration": "1.000000",
                    "size": "20480",
                    "bit_rate": "163840",
                    "tags": {
                        "{{titleKey}}": "FLAC Test Title",
                        "{{artistKey}}": "FLAC Test Artist",
                        "{{albumKey}}": "FLAC Test Album",
                        "{{dateKey}}": "2024-09-15",
                        "{{genreKey}}": "Rock"
                    }
                }
            }
            """;
        var probeData = JsonSerializer.Deserialize<FFprobeOutput>(output)!;

        var metadata = MediaPreviewService.MapMetadata(probeData);

        metadata.Title.Should().Be("FLAC Test Title");
        metadata.Artist.Should().Be("FLAC Test Artist");
        metadata.Album.Should().Be("FLAC Test Album");
        metadata.Year.Should().Be("2024-09-15");
        metadata.Genre.Should().Be("Rock");

        metadata.FormatName.Should().Be("flac");
        metadata.AudioCodec.Should().Be("flac");
        metadata.HasAudio.Should().BeTrue();
        metadata.HasVideo.Should().BeFalse();
        metadata.FileSize.Should().Be(20480);
    }

    [Theory]
    [InlineData("null")]
    [InlineData("{}")]
    public void MapMetadata_noTags_leavesDescriptiveFieldsEmpty(string tags)
    {
        var output = $$"""
            {
                "streams": [{ "codec_name": "flac", "codec_type": "audio" }],
                "format": { "format_name": "flac", "tags": {{tags}} }
            }
            """;
        var probeData = JsonSerializer.Deserialize<FFprobeOutput>(output)!;

        var metadata = MediaPreviewService.MapMetadata(probeData);

        metadata.Title.Should().BeNull();
        metadata.Artist.Should().BeNull();
        metadata.Album.Should().BeNull();
        metadata.Year.Should().BeNull();
        metadata.Genre.Should().BeNull();
        metadata.HasAudio.Should().BeTrue();
    }
}
