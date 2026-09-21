namespace Alexandria.Common.Exceptions.Streaming;

public sealed class StreamingAnchorNotFoundException(string? message = null)
    : Exception(message ?? "The requested playback anchor is not part of the eligible source.");