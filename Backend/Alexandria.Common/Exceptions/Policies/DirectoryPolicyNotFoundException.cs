namespace Alexandria.Common.Exceptions.Policies;

public sealed class DirectoryPolicyNotFoundException(Guid directoryId)
    : Exception($"No policy found for directory {directoryId}.");