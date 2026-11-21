using Alexandria.Data.Models.Enumerators;

namespace Alexandria.Dto.Files;

/// <summary>
/// What reading one document produced. <see cref="Text"/> is already normalized and capped, and
/// is kept even for <see cref="DocumentTextStatus.NoTextLayer"/>, because the few words a scan
/// carries (a stamped title, a page number) can still be worth finding.
/// </summary>
public sealed record DocumentTextResult(DocumentTextStatus Status, string Text, bool IsTruncated);