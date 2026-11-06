namespace Alexandria.Common.Processes;

/// <summary>
/// The outcome of a finished external process. <see cref="StdOut"/> is capped and
/// <see cref="StdErrTail"/> holds only the end of the stream.
/// </summary>
public sealed record ExternalProcessResult(int ExitCode, string StdOut, string StdErrTail);
