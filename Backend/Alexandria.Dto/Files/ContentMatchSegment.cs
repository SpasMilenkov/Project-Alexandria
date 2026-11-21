namespace Alexandria.Dto.Files;

/// <summary>
/// A run of document text, either one of the searched words or the context around it. Sent as
/// plain text so the client never renders markup that came from a document.
/// </summary>
public sealed record ContentMatchSegment(string Text, bool IsMatch);