namespace Alexandria.Common.Exceptions.Streaming;

public sealed class StreamSessionNotFoundException(Guid id)
    : Exception($"Stream session with id {id} was not found.");