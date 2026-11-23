using System.Text;
using Alexandria.Common.Exceptions.Files;
using Alexandria.Common.Notes;
using Blake3;

namespace Alexandria.Services.Storage.Content;

/// <summary>How editor text becomes stored bytes, shared by versions and drafts so both compare alike.</summary>
internal static class TextContentBytes
{
    // No byte order mark, so a saved note hashes the same as the same text uploaded from disk.
    private static readonly UTF8Encoding TextEncoding = new(encoderShouldEmitUTF8Identifier: false);

    /// <exception cref="FileContentNotEditableException">The text is over the editor's limit.</exception>
    public static byte[] Encode(Guid fileId, string text)
    {
        var content = TextEncoding.GetBytes(text);

        if (content.Length > TextContentLimits.MaxBytes)
            throw new FileContentNotEditableException(fileId, "The text is too large to save.");

        return content;
    }

    public static byte[] Hash(byte[] content) => Hasher.Hash(content).AsSpan().ToArray();
}
