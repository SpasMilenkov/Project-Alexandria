namespace Alexandria.Common.Services;

public sealed record FileFinalizedEvent(
    Guid FileId,
    Guid? VersionId,
    Guid? DirectoryId,
    string MimeType,
    string FileName,
    Guid OwnerId,
    bool IsNewVersion
);

public interface IPolicyDispatcher
{
    Task DispatchAsync(FileFinalizedEvent ev, CancellationToken ct = default);
}