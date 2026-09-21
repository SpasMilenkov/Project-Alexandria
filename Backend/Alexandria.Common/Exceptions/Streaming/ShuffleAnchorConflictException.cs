namespace Alexandria.Common.Exceptions.Streaming;

public sealed class ShuffleAnchorConflictException(string? message = null)
    : Exception(message ?? "The requested anchor is not part of the eligible shuffle source.");