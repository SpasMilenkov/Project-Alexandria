namespace Alexandria.Dto.DocumentTextStats;

/// <summary>
/// A document whose text could not be read.
/// </summary>
/// <param name="FileName">
/// The file's name when the viewer owns it, and null for another account's file, whose name an
/// admin is never shown.
/// </param>
/// <param name="MimeType">The file's type when the viewer owns it, otherwise null.</param>
/// <param name="ErrorDetail">
/// Why reading failed, when the viewer owns the file. Null for another account's file, because the
/// message can quote storage keys or parts of the document.
/// </param>
/// <param name="WillRetry">False once the sweep has used up its attempts for this document.</param>
public sealed record DocumentTextFailure(
    Guid ContentObjectId,
    string? FileName,
    string? MimeType,
    string? ErrorDetail,
    int AttemptCount,
    DateTime? LastAttemptAt,
    bool WillRetry);