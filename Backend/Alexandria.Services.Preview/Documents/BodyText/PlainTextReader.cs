using System.Text;

namespace Alexandria.Services.Preview.Documents.BodyText;

/// <summary>
/// Reads plain text and Markdown as UTF-8, honouring a byte order mark. Markdown syntax is left
/// in: its symbols are separators to the search parser, so they never become words.
/// </summary>
internal static class PlainTextReader
{
    private const int ChunkChars = 8 * 1024;

    public static void Read(Stream source, TextCollector collector)
    {
        using var reader = new StreamReader(source, Encoding.UTF8, detectEncodingFromByteOrderMarks: true,
            bufferSize: ChunkChars, leaveOpen: true);

        var buffer = new char[ChunkChars];
        int read;

        while (!collector.IsFull && (read = reader.Read(buffer, 0, buffer.Length)) > 0)
            collector.Append(buffer.AsSpan(0, read));
    }
}