namespace Alexandria.Common.Exceptions;

public sealed class CapabilityUnavailableException(string capability)
    : InvalidOperationException($"{capability} is disabled for this installation.");
