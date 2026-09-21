namespace Alexandria.Common.Exceptions.Streaming;

public sealed class ShuffleOffsetOutOfRangeException(int offset, int totalCount)
    : Exception($"Shuffle offset {offset} is beyond snapshot size {totalCount}.");