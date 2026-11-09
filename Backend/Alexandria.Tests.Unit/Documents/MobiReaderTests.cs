using System.Buffers.Binary;
using System.Text;
using Alexandria.Services.Preview.Documents.Metadata;
using AwesomeAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace Alexandria.Tests.Unit.Documents;

public class MobiReaderTests
{
    private const int Utf8 = 65001;
    private const int Windows1252 = 1252;
    private const int Kf8HeaderLength = 264;

    // The Palm header and a two-entry record list, then record 0 at a fixed offset.
    private const int RecordZeroOffset = 78 + 2 * 8 + 2;

    private static readonly (uint Type, byte[] Data)[] CrusoeExth =
    [
        (503, "The Life and Adventures of Robinson Crusoe"u8.ToArray()),
        (100, "Daniel Defoe"u8.ToArray()),
        (101, "Standard Ebooks"u8.ToArray()),
        (105, "Islands--Fiction"u8.ToArray()),
        (105, "Adventure stories"u8.ToArray()),
        (106, "2017-12-09T18:37:23+00:00"u8.ToArray()),
        (104, "978-0-306-40615-7"u8.ToArray()),
        (524, "en"u8.ToArray()),
        (204, [0, 0, 0, 201])
    ];

    [Fact]
    public void Read_kf8Book_readsTitleAndTextExthRecords()
    {
        var book = BuildBook(fullName: "Robinson Crusoe", exth: CrusoeExth);

        var fields = Read(book).Values;

        fields["mobi:title"].Should().Be("Robinson Crusoe");
        fields["mobi:version"].Should().Be("8");
        fields["exth:title"].Should().Be("The Life and Adventures of Robinson Crusoe");
        fields["exth:author"].Should().Be("Daniel Defoe");
        fields["exth:publisher"].Should().Be("Standard Ebooks");
        fields["exth:subject"].Should().Be("Islands--Fiction; Adventure stories");
        fields["exth:published"].Should().Be("2017-12-09T18:37:23+00:00");
        fields["exth:isbn"].Should().Be("978-0-306-40615-7");
        fields["exth:language"].Should().Be("en");
        fields.Should().NotContainKey("mobi:drm");
        fields.Keys.Should().NotContain(key => key.Contains("204"), "numeric creator records are not text");
    }

    [Fact]
    public void Read_encryptedBook_isFlaggedButKeepsItsHeaders()
    {
        var book = BuildBook(fullName: "Locked novel", exth: [(100, "Ana Doe"u8.ToArray())], encryption: 2);

        var fields = Read(book).Values;

        fields["mobi:drm"].Should().Be("mobipocket");
        fields["mobi:title"].Should().Be("Locked novel");
        fields["exth:author"].Should().Be("Ana Doe");
    }

    [Fact]
    public void Read_windows1252Book_decodesCurlyQuotes()
    {
        // 0x93 and 0x94 are the curly double quotes in Windows-1252 and control codes in Latin-1.
        byte[] title = [0x93, (byte)'H', (byte)'i', 0x94];
        var book = BuildBook(fullName: "x", exth: [(503, title)], textEncoding: Windows1252);

        var fields = Read(book).Values;

        fields["exth:title"].Should().Be("“Hi”");
    }

    [Fact]
    public void Read_malformedExthRecord_keepsTheRecordsBeforeIt()
    {
        var book = BuildBook(fullName: "x", exth: [(100, "Daniel Defoe"u8.ToArray()), (101, "Standard Ebooks"u8.ToArray())]);
        var secondLengthField = ExthStart() + 12 + 8 + "Daniel Defoe".Length + 4;
        BinaryPrimitives.WriteUInt32BigEndian(book.AsSpan(secondLengthField), 100_000);

        var fields = Read(book).Values;

        fields["exth:author"].Should().Be("Daniel Defoe");
        fields.Should().NotContainKey("exth:publisher");
    }

    [Fact]
    public void Read_withoutExthFlag_readsOnlyTheHeaderTitle()
    {
        var book = BuildBook(fullName: "Robinson Crusoe", exth: CrusoeExth, exthFlag: false);

        var fields = Read(book).Values;

        fields["mobi:title"].Should().Be("Robinson Crusoe");
        fields.Keys.Should().NotContain(key => key.StartsWith("exth:"));
    }

    [Fact]
    public void Read_notAMobipocketBook_throws()
    {
        var book = BuildBook(fullName: "x", exth: []);
        "TEXtREAd"u8.CopyTo(book.AsSpan(60));

        var act = () => Read(book);

        act.Should().Throw<InvalidDataException>();
    }

    [Fact]
    public void Read_recordListPointingPastTheFile_throws()
    {
        var book = BuildBook(fullName: "x", exth: []);
        BinaryPrimitives.WriteUInt32BigEndian(book.AsSpan(78), (uint)book.Length + 10);

        var act = () => Read(book);

        act.Should().Throw<InvalidDataException>();
    }

    private static DocumentFields Read(byte[] book)
    {
        using var stream = new MemoryStream(book);

        return MobiReader.Read(stream, NullLogger.Instance);
    }

    private static int ExthStart() => RecordZeroOffset + 16 + Kf8HeaderLength;

    /// <summary>
    /// A two-record Palm database whose first record holds the PalmDOC header, a KF8-sized MOBI
    /// header, the EXTH block and the full name, in the order writers lay them out.
    /// </summary>
    private static byte[] BuildBook(
        string fullName,
        (uint Type, byte[] Data)[] exth,
        ushort encryption = 0,
        int textEncoding = Utf8,
        bool exthFlag = true)
    {
        var exthBody = new MemoryStream();
        foreach (var (type, data) in exth)
        {
            WriteUInt32(exthBody, type);
            WriteUInt32(exthBody, (uint)(8 + data.Length));
            exthBody.Write(data);
        }

        var exthBlock = new MemoryStream();
        exthBlock.Write("EXTH"u8);
        WriteUInt32(exthBlock, (uint)(12 + exthBody.Length));
        WriteUInt32(exthBlock, (uint)exth.Length);
        exthBlock.Write(exthBody.ToArray());

        var name = Encoding.UTF8.GetBytes(fullName);
        var nameOffset = 16 + Kf8HeaderLength + (int)exthBlock.Length;
        var record = new byte[nameOffset + name.Length + 2];

        BinaryPrimitives.WriteUInt16BigEndian(record.AsSpan(12), encryption);
        "MOBI"u8.CopyTo(record.AsSpan(16));
        BinaryPrimitives.WriteUInt32BigEndian(record.AsSpan(20), Kf8HeaderLength);
        BinaryPrimitives.WriteUInt32BigEndian(record.AsSpan(28), (uint)textEncoding);
        BinaryPrimitives.WriteUInt32BigEndian(record.AsSpan(36), 8);
        BinaryPrimitives.WriteUInt32BigEndian(record.AsSpan(84), (uint)nameOffset);
        BinaryPrimitives.WriteUInt32BigEndian(record.AsSpan(88), (uint)name.Length);
        BinaryPrimitives.WriteUInt32BigEndian(record.AsSpan(128), exthFlag ? 0x50u : 0x10u);
        exthBlock.ToArray().CopyTo(record, 16 + Kf8HeaderLength);
        name.CopyTo(record, nameOffset);

        var secondRecord = "text"u8.ToArray();
        var book = new byte[RecordZeroOffset + record.Length + secondRecord.Length];

        "Robinson_Crusoe"u8.CopyTo(book);
        "BOOKMOBI"u8.CopyTo(book.AsSpan(60));
        BinaryPrimitives.WriteUInt16BigEndian(book.AsSpan(76), 2);
        BinaryPrimitives.WriteUInt32BigEndian(book.AsSpan(78), RecordZeroOffset);
        BinaryPrimitives.WriteUInt32BigEndian(book.AsSpan(86), (uint)(RecordZeroOffset + record.Length));
        record.CopyTo(book, RecordZeroOffset);
        secondRecord.CopyTo(book, RecordZeroOffset + record.Length);

        return book;
    }

    private static void WriteUInt32(Stream stream, uint value)
    {
        Span<byte> buffer = stackalloc byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(buffer, value);
        stream.Write(buffer);
    }
}
