namespace Alexandria.Services.Storage.Content.Mime;

/// <summary>
/// Recognizes Mobipocket and Kindle books by their Palm database type. These files open with the
/// book's title as plain text, which MimeDetective's default definitions mistake for an email, and
/// they carry no other signature. The extension then picks the family member: <c>.azw3</c> for
/// KF8, <c>.mobi</c> for the older format.
/// </summary>
internal static class MobipocketSignature
{
    private const string Mobipocket = "application/x-mobipocket-ebook";

    // The type and creator codes sit after the 32-byte name and seven header fields.
    private const int TypeOffset = 60;

    internal static IEnumerable<string> AllTypes => [Mobipocket];

    /// <summary>The Mobipocket type when the header is a Palm database of type BOOK/MOBI, else null.</summary>
    public static string? Detect(byte[] header)
    {
        if (header.Length < TypeOffset + 8) return null;

        return header.AsSpan(TypeOffset, 8).SequenceEqual("BOOKMOBI"u8) ? Mobipocket : null;
    }
}
