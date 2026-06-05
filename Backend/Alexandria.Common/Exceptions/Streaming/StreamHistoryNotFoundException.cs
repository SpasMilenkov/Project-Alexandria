namespace Alexandria.Common.Exceptions.Streaming;

public sealed class StreamHistoryNotFoundException(Guid id)
    : Exception($"Stream history with id {id} was not found.");