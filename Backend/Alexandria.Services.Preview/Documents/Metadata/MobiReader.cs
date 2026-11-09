using System.Buffers.Binary;
using System.Text;
using Microsoft.Extensions.Logging;

namespace Alexandria.Services.Preview.Documents.Metadata;

/// <summary>
/// Reads the first record of a Mobipocket or Kindle (KF8) book. That record holds the PalmDOC
/// header with the encryption flag, the MOBI header with the full title and text encoding, and
/// the EXTH block where authors, publisher, subjects and the ISBN live. Copy protection is
/// recognized but does not stop the read, because stores leave this record unencrypted.
/// </summary>
internal static partial class MobiReader
{
    private const int PalmHeaderLength = 78;
    private const int RecordCountOffset = 76;
    private const int RecordEntryLength = 8;
    private const int TypeOffset = 60;

    private const int EncryptionOffset = 12;
    private const int MobiHeaderOffset = 16;
    private const int HeaderLengthOffset = 20;
    private const int TextEncodingOffset = 28;
    private const int FormatVersionOffset = 36;
    private const int FullNameOffset = 84;
    private const int FullNameLengthOffset = 88;
    private const int ExthFlagsOffset = 128;
    private const int ExthPresent = 0x40;
    private const int ExthRecordHeaderLength = 8;
    private const int Utf8CodePage = 65001;

    /// <summary>
    /// The first record carries headers only, a few kilobytes even with a long description. Past
    /// this it is broken or hostile.
    /// </summary>
    internal const int MaxFirstRecordBytes = 1024 * 1024;

    private static readonly Encoding Windows1252 = CodePagesEncodingProvider.Instance.GetEncoding(1252)!;

    // Types the format leaves as text. The numeric ones (cover offsets, creator build numbers)
    // describe how the file was produced, and calibre fakes those to look like kindlegen.
    private static readonly Dictionary<uint, string> TextRecords = new()
    {
        [100] = "exth:author",
        [101] = "exth:publisher",
        [103] = "exth:description",
        [104] = "exth:isbn",
        [105] = "exth:subject",
        [106] = "exth:published",
        [108] = "exth:contributor",
        [109] = "exth:rights",
        [112] = "exth:source",
        [113] = "exth:asin",
        [501] = "exth:document-type",
        [503] = "exth:title",
        [524] = "exth:language"
    };

    public static DocumentFields Read(Stream source, ILogger logger)
    {
        var record = ReadFirstRecord(source);
        var fields = new DocumentFieldBag();

        fields.Add("mobi:drm", DrmScheme(BinaryPrimitives.ReadUInt16BigEndian(record.AsSpan(EncryptionOffset))));

        // A plain PalmDOC book stops after the compression header and has nothing else to read.
        if (record.Length < HeaderLengthOffset + 4 || !record.AsSpan(MobiHeaderOffset, 4).SequenceEqual("MOBI"u8))
            return fields.ToFields();

        var headerLength = ReadUInt32(record, HeaderLengthOffset) ?? 0;
        var encoding = ReadUInt32(record, TextEncodingOffset) == Utf8CodePage ? Encoding.UTF8 : Windows1252;

        fields.Add("mobi:version", ReadUInt32(record, FormatVersionOffset)?.ToString());
        fields.Add("mobi:title", FullName(record, encoding));

        // Older, shorter MOBI headers end before the flags field, and those books have no EXTH block.
        var headerEnd = MobiHeaderOffset + headerLength;
        var flags = headerEnd >= ExthFlagsOffset + 4 ? ReadUInt32(record, ExthFlagsOffset) ?? 0 : 0;
        if ((flags & ExthPresent) != 0)
            ReadExth(record, MobiHeaderOffset + headerLength, encoding, fields, logger);

        return fields.ToFields();
    }

    /// <summary>
    /// Locates the first record through the Palm database's record list. Its end is where the
    /// second record starts, or the end of the file for a single-record book.
    /// </summary>
    private static byte[] ReadFirstRecord(Stream source)
    {
        var header = new byte[PalmHeaderLength + 2 * RecordEntryLength];
        var read = source.ReadAtLeast(header, header.Length, throwOnEndOfStream: false);

        if (read < PalmHeaderLength + RecordEntryLength || !header.AsSpan(TypeOffset, 8).SequenceEqual("BOOKMOBI"u8))
            throw new InvalidDataException("Not a Mobipocket book.");

        var recordCount = BinaryPrimitives.ReadUInt16BigEndian(header.AsSpan(RecordCountOffset));
        if (recordCount == 0)
            throw new InvalidDataException("The book lists no records.");

        long start = BinaryPrimitives.ReadUInt32BigEndian(header.AsSpan(PalmHeaderLength));
        long end = source.Length;
        if (recordCount > 1 && read >= header.Length)
            end = BinaryPrimitives.ReadUInt32BigEndian(header.AsSpan(PalmHeaderLength + RecordEntryLength));

        var listEnd = PalmHeaderLength + (long)recordCount * RecordEntryLength;
        if (start < listEnd || end <= start || end > source.Length)
            throw new InvalidDataException("The book's record list points outside the file.");

        var length = (int)Math.Min(end - start, MaxFirstRecordBytes);
        if (length < MobiHeaderOffset)
            throw new InvalidDataException("The book's first record is too short.");

        var record = new byte[length];
        source.Seek(start, SeekOrigin.Begin);
        source.ReadExactly(record);

        return record;
    }

    private static string? DrmScheme(ushort encryption)
    {
        if (encryption == 0)
            return null;

        if (encryption == 1)
            return "mobipocket-legacy";

        if (encryption == 2)
            return "mobipocket";

        return "unknown";
    }

    private static string? FullName(byte[] record, Encoding encoding)
    {
        if (ReadUInt32(record, FullNameOffset) is not { } offset || ReadUInt32(record, FullNameLengthOffset) is not { } length)
            return null;

        if (length == 0 || offset + length > record.Length)
            return null;

        return Decode(record.AsSpan((int)offset, (int)length), encoding);
    }

    private static void ReadExth(byte[] record, long start, Encoding encoding, DocumentFieldBag fields, ILogger logger)
    {
        if (start + 12 > record.Length || !record.AsSpan((int)start, 4).SequenceEqual("EXTH"u8))
            return;

        var blockLength = ReadUInt32(record, start + 4) ?? 0;
        var count = ReadUInt32(record, start + 8) ?? 0;
        var end = Math.Min(record.Length, start + blockLength);
        var position = start + 12;

        for (var index = 0; index < count; index++)
        {
            var type = ReadUInt32(record, position);
            var length = ReadUInt32(record, position + 4) ?? 0;

            if (type is null || length < ExthRecordHeaderLength || position + length > end)
            {
                LogExthTruncated(logger, index, count);
                return;
            }

            if (TextRecords.TryGetValue((uint)type.Value, out var key))
            {
                var data = record.AsSpan((int)(position + ExthRecordHeaderLength), (int)(length - ExthRecordHeaderLength));
                fields.Add(key, Decode(data, encoding));
            }

            position += length;
        }
    }

    // Writers pad fixed-size strings with nulls, which would survive a whitespace trim.
    private static string Decode(ReadOnlySpan<byte> bytes, Encoding encoding)
        => encoding.GetString(bytes).TrimEnd('\0');

    private static long? ReadUInt32(byte[] buffer, long offset)
    {
        if (offset < 0 || offset + 4 > buffer.Length)
            return null;

        return BinaryPrimitives.ReadUInt32BigEndian(buffer.AsSpan((int)offset));
    }

    [LoggerMessage(4276, LogLevel.Warning, "Stopped reading a malformed MOBI EXTH block at record {Index} of {Count}")]
    private static partial void LogExthTruncated(ILogger logger, int index, long count);
}
