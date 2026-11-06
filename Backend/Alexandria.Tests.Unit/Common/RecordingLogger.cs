using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace Alexandria.Tests.Unit.Common;

/// <summary>
/// Keeps every log entry so a test can assert on what was, or was not, logged.
/// </summary>
internal sealed class RecordingLogger<T> : ILogger<T>
{
    public ConcurrentBag<(LogLevel Level, string Message)> Entries { get; } = [];

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(
        LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter) =>
        Entries.Add((logLevel, formatter(state, exception)));
}
