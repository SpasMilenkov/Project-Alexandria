namespace Alexandria.Common.Exceptions;

public class StreamingRepresentationNotFoundException(Guid id)
    : Exception($"Streaming representation '{id}' was not found.");