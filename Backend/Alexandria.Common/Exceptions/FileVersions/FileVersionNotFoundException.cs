namespace Alexandria.Common.Exceptions.FileVersions;

public sealed class FileVersionNotFoundException(Guid versionId)
    : Exception($"File version {versionId} was not found or does not belong to the specified file.");