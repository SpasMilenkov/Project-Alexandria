namespace Alexandria.Dto.Files;

/// <summary>The stored bytes of a text version, read for the editor rather than downloaded.</summary>
public sealed record VersionText(Guid FileId, Guid VersionId, string MimeType, byte[] Content);
