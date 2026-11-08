using Alexandria.Common.Services;
using Alexandria.Services.Preview.Images;
using AwesomeAssertions;

namespace Alexandria.Tests.Unit.PreviewJobs;

public class CaptureMetadataMapperTests
{
    private static readonly Guid VersionId = Guid.NewGuid();

    private static CaptureHeader Header(params (string Name, string Value)[] fields) => new()
    {
        Width = 4032,
        Height = 3024,
        Fields = fields.ToDictionary(f => f.Name, f => f.Value)
    };

    [Fact]
    public void capture_time_with_offset_is_converted_to_the_true_instant()
    {
        var entity = CaptureMetadataMapper.ToEntity(VersionId, Header(
            ("exif-ifd2-DateTimeOriginal", "2026:11:07 20:34:29"),
            ("exif-ifd2-SubSecTimeOriginal", "714"),
            ("exif-ifd2-OffsetTimeOriginal", "+02:00")));

        entity.CapturedAt.Should().Be(new DateTime(2026, 11, 7, 18, 34, 29, 714, DateTimeKind.Utc));
        entity.CapturedAt!.Value.Kind.Should().Be(DateTimeKind.Utc);
        entity.CapturedOffsetMinutes.Should().Be(120);
    }

    [Fact]
    public void capture_time_without_offset_keeps_the_wall_clock_and_a_null_offset()
    {
        var entity = CaptureMetadataMapper.ToEntity(VersionId, Header(
            ("exif-ifd2-DateTimeOriginal", "2026:08:14 18:22:31")));

        entity.CapturedAt.Should().Be(new DateTime(2026, 8, 14, 18, 22, 31, DateTimeKind.Utc));
        entity.CapturedOffsetMinutes.Should().BeNull();
    }

    [Fact]
    public void negative_offset_moves_the_instant_forward()
    {
        var entity = CaptureMetadataMapper.ToEntity(VersionId, Header(
            ("exif-ifd2-DateTimeOriginal", "2026:01:01 23:30:00"),
            ("exif-ifd2-OffsetTimeOriginal", "-05:30")));

        entity.CapturedAt.Should().Be(new DateTime(2026, 1, 2, 5, 0, 0, DateTimeKind.Utc));
        entity.CapturedOffsetMinutes.Should().Be(-330);
    }

    [Theory]
    [InlineData("0000:00:00 00:00:00")]
    [InlineData("    :  :     :  :  ")]
    [InlineData("0001:01:01 00:00:00")]
    public void placeholder_capture_time_is_treated_as_missing(string value)
    {
        var entity = CaptureMetadataMapper.ToEntity(VersionId, Header(("exif-ifd2-DateTimeOriginal", value)));

        entity.CapturedAt.Should().BeNull();
    }

    [Fact]
    public void edit_time_is_never_used_as_capture_time()
    {
        var entity = CaptureMetadataMapper.ToEntity(VersionId, Header(("exif-ifd0-DateTime", "2026:08:14 18:22:31")));

        entity.CapturedAt.Should().BeNull();
    }

    [Fact]
    public void digitized_time_is_the_fallback_when_original_is_missing()
    {
        var entity = CaptureMetadataMapper.ToEntity(VersionId, Header(
            ("exif-ifd2-DateTimeDigitized", "2026:08:14 18:22:31"),
            ("exif-ifd2-OffsetTimeDigitized", "+01:00")));

        entity.CapturedAt.Should().Be(new DateTime(2026, 8, 14, 17, 22, 31, DateTimeKind.Utc));
        entity.CapturedOffsetMinutes.Should().Be(60);
    }

    [Fact]
    public void location_is_converted_to_signed_decimal_degrees()
    {
        var entity = CaptureMetadataMapper.ToEntity(VersionId, Header(
            ("exif-ifd3-GPSLatitudeRef", "S"),
            ("exif-ifd3-GPSLatitude", "42/1 30/1 0/1"),
            ("exif-ifd3-GPSLongitudeRef", "W"),
            ("exif-ifd3-GPSLongitude", "23/1 15/1 36/1")));

        entity.Latitude.Should().BeApproximately(-42.5, 0.000001);
        entity.Longitude.Should().BeApproximately(-23.26, 0.000001);
    }

    [Theory]
    [InlineData("", "42/1 30/1 0/1", "E", "23/1 15/1 0/1")]
    [InlineData("N", "42/1 30/1 0/0", "E", "23/1 15/1 0/1")]
    [InlineData("N", "42/1 30/1", "E", "23/1 15/1 0/1")]
    [InlineData("N", "91/1 0/1 0/1", "E", "23/1 15/1 0/1")]
    [InlineData("N", "42/1 30/1 0/1", "E", "181/1 0/1 0/1")]
    [InlineData("N", "0/1 0/1 0/1", "E", "0/1 0/1 0/1")]
    public void unusable_location_leaves_both_coordinates_null(
        string latitudeRef, string latitude, string longitudeRef, string longitude)
    {
        var entity = CaptureMetadataMapper.ToEntity(VersionId, Header(
            ("exif-ifd3-GPSLatitudeRef", latitudeRef),
            ("exif-ifd3-GPSLatitude", latitude),
            ("exif-ifd3-GPSLongitudeRef", longitudeRef),
            ("exif-ifd3-GPSLongitude", longitude)));

        entity.Latitude.Should().BeNull();
        entity.Longitude.Should().BeNull();
    }

    [Fact]
    public void exposure_values_are_parsed_from_rationals()
    {
        var entity = CaptureMetadataMapper.ToEntity(VersionId, Header(
            ("exif-ifd2-ExposureTime", "1/15"),
            ("exif-ifd2-FNumber", "8/5"),
            ("exif-ifd2-FocalLength", "51/10"),
            ("exif-ifd2-FocalLengthIn35mmFilm", "26"),
            ("exif-ifd2-ISOSpeedRatings", "8000")));

        entity.ExposureSeconds.Should().BeApproximately(1.0 / 15, 0.000001);
        entity.FNumber.Should().Be(1.6);
        entity.FocalLengthMm.Should().Be(5.1);
        entity.FocalLength35mm.Should().Be(26);
        entity.Iso.Should().Be(8000);
    }

    [Fact]
    public void zero_means_unknown_for_integer_tags_and_division_by_zero_is_null()
    {
        var entity = CaptureMetadataMapper.ToEntity(VersionId, Header(
            ("exif-ifd2-FocalLengthIn35mmFilm", "0"),
            ("exif-ifd2-ISOSpeedRatings", "0"),
            ("exif-ifd2-FNumber", "0/0")));

        entity.FocalLength35mm.Should().BeNull();
        entity.Iso.Should().BeNull();
        entity.FNumber.Should().BeNull();
    }

    [Theory]
    [InlineData(1, 4032, 3024)]
    [InlineData(3, 4032, 3024)]
    [InlineData(5, 3024, 4032)]
    [InlineData(6, 3024, 4032)]
    [InlineData(8, 3024, 4032)]
    public void quarter_turn_orientations_swap_the_display_dimensions(int orientation, int width, int height)
    {
        var header = Header() with { Orientation = orientation };

        var entity = CaptureMetadataMapper.ToEntity(VersionId, header);

        entity.Width.Should().Be(width);
        entity.Height.Should().Be(height);
    }

    [Fact]
    public void empty_header_yields_a_row_that_stops_the_backfill_from_reading_it_again()
    {
        var entity = CaptureMetadataMapper.ToEntity(VersionId, new CaptureHeader());

        entity.FileVersionId.Should().Be(VersionId);
        entity.Width.Should().BeNull();
        entity.CapturedAt.Should().BeNull();
        entity.RawJson.Should().Be("{}");
        entity.ExtractorVersion.Should().Be(CaptureMetadataExtractor.CurrentVersion);
    }

    [Fact]
    public void long_text_is_truncated_to_the_column_length()
    {
        var entity = CaptureMetadataMapper.ToEntity(VersionId, Header(("exif-ifd0-Make", new string('x', 400))));

        entity.CameraMake.Should().HaveLength(255);
    }
}
