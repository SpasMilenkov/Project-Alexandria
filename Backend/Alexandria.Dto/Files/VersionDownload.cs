namespace Alexandria.Dto.Files;

/// <summary>
/// What a version download hands the client, plus where the version sits, which the download's
/// audit row needs and the response does not carry.
/// </summary>
public sealed record VersionDownload(DownloadInfo Info, Guid FileId, int VersionNumber);
