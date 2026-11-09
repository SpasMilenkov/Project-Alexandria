using System.Buffers.Text;
using System.Text;
using Alexandria.Data.Models.Enumerators;
using Alexandria.Dto.Gallery;
using AwesomeAssertions;

namespace Alexandria.Tests.Unit.Gallery;

public class GalleryCursorCodecTests
{
    private static readonly GalleryCursor Cursor = new()
    {
        // Microsecond precision is what Postgres hands back, and what must survive the trip.
        SortDate = new DateTime(2019, 6, 12, 18, 15, 42, DateTimeKind.Utc).AddTicks(1_234_560),
        FileId = Guid.Parse("0fb982cc-5c8d-4abe-9598-58d5730cee2f"),
    };

    private static string Raw(string value) => Base64Url.EncodeToString(Encoding.UTF8.GetBytes(value));

    [Theory]
    [InlineData(GallerySort.Taken)]
    [InlineData(GallerySort.Uploaded)]
    public void Decode_EncodedCursor_RoundTripsExactly(GallerySort sort)
    {
        var encoded = GalleryCursorCodec.Encode(Cursor, sort);

        var decoded = GalleryCursorCodec.TryDecode(encoded, sort, out var cursor);

        decoded.Should().BeTrue();
        cursor.Should().Be(Cursor);
        cursor!.SortDate.Kind.Should().Be(DateTimeKind.Utc);
    }

    [Fact]
    public void Decode_CursorFromOtherSort_IsRejected()
    {
        var encoded = GalleryCursorCodec.Encode(Cursor, GallerySort.Taken);

        GalleryCursorCodec.TryDecode(encoded, GallerySort.Uploaded, out var cursor).Should().BeFalse();
        cursor.Should().BeNull();
    }

    [Fact]
    public void Encode_Output_IsUrlSafe()
    {
        var encoded = GalleryCursorCodec.Encode(Cursor, GallerySort.Taken);

        encoded.Should().MatchRegex("^[A-Za-z0-9_-]+$");
    }

    [Theory]
    [InlineData("")]
    [InlineData("not base64 !")]
    [InlineData("MjAxOS0wNi0xMg")]
    public void Decode_Garbage_IsRejected(string value)
    {
        GalleryCursorCodec.TryDecode(value, GallerySort.Taken, out _).Should().BeFalse();
    }

    [Theory]
    [InlineData("0.-5.0fb982cc5c8d4abe959858d5730cee2f")]
    [InlineData("0.99999999999999999999.0fb982cc5c8d4abe959858d5730cee2f")]
    [InlineData("0.637000000000000000.not-a-guid")]
    [InlineData("7.637000000000000000.0fb982cc5c8d4abe959858d5730cee2f")]
    [InlineData("0.637000000000000000.0fb982cc5c8d4abe959858d5730cee2f.extra")]
    public void Decode_TamperedPayload_IsRejected(string payload)
    {
        GalleryCursorCodec.TryDecode(Raw(payload), GallerySort.Taken, out _).Should().BeFalse();
    }
}