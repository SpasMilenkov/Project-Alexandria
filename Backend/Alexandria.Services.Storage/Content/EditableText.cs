using Alexandria.Common.Exceptions.Files;
using Alexandria.Common.Mime;
using Alexandria.Common.Notes;
using Alexandria.Data.Models;

namespace Alexandria.Services.Storage.Content;

/// <summary>
/// Which versions editor text may build on. Drafts and saves keep the base version's MIME type,
/// so text written over anything else would leave, say, a video whose bytes are a note. The idle
/// sweep cuts versions with nobody watching, so the check can't be left to the client.
/// </summary>
internal static class EditableText
{
    /// <exception cref="FileContentNotEditableException">Encrypted, not text, or too large to open.</exception>
    public static void EnsureEditable(FileVersion version)
    {
        if (version.IsEncrypted)
            throw new FileContentNotEditableException(version.Id, "Encrypted versions can't be edited as text.");

        if (!MimeCategories.IsText(version.MimeType))
            throw new FileContentNotEditableException(version.Id, "Only text files can be edited as text.");

        // The editor never opens a version this large, so text claiming to build on it didn't.
        if (version.Size > TextContentLimits.MaxBytes)
            throw new FileContentNotEditableException(version.Id, "This version is too large to edit as text.");
    }
}
