namespace Alexandria.Common.Exceptions.Streaming;

public sealed class ShuffleSessionNotFoundException(Guid id)
    : Exception($"Shuffle session with id {id} was not found.");