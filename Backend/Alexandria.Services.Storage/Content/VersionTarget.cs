namespace Alexandria.Services.Storage.Content;

/// <summary>
/// Where an editor save lands: a file addressed by id, the version the edit started from, and the
/// MIME type that version had, which the new version keeps whatever the file is called now.
/// </summary>
internal sealed record VersionTarget(Guid FileId, Guid ParentVersionId, string MimeType);
