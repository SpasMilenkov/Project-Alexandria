namespace Alexandria.Common.Exceptions.Streaming;

public sealed class ShuffleRequestConflictException(Guid requestId)
    : Exception($"Request {requestId} was already used with different shuffle inputs.");