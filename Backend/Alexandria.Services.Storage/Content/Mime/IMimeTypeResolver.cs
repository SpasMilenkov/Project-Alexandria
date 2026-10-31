using Alexandria.Common.Mime;

namespace Alexandria.Services.Storage.Content.Mime;

/// <summary>
/// Decides the MIME type to store for a file. Bytes win over the file name, and the file name
/// wins over the client-declared type. Implementations are pure, deterministic and never throw.
/// Every returned MIME type is canonical (see <see cref="MimeAliases"/>).
/// </summary>
public interface IMimeTypeResolver
{
    /// <summary>
    /// Resolves the type of a plaintext file from its header bytes, its name and the declared type.
    /// </summary>
    /// <param name="fileName">The user-visible file name. Only the extension is used.</param>
    /// <param name="header">The first <see cref="MimeSniffing.HeaderBytes"/> bytes of the file, possibly empty.</param>
    /// <param name="declared">The client-declared type. Treated as a hint and may be null or unusable.</param>
    MimeResolution Resolve(string fileName, byte[] header, string? declared = null);

    /// <summary>
    /// Resolves the type of a file whose content must not be inspected (encrypted uploads).
    /// Uses the declared type, then the extension, then application/octet-stream.
    /// </summary>
    MimeResolution ResolveWithoutContent(string fileName, string? declared);
}
