namespace Alexandria.Common.Exceptions.Streaming;

public sealed class StreamSessionAlreadyClosedException(Guid id)
    : Exception($"Stream session {id} has already been closed.");