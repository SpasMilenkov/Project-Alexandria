namespace Alexandria.Common.Exceptions;

/// <summary>
/// The cursor was not produced by this server for the requested sort. The endpoint validator
/// rejects these first, so reaching the service with one means a caller skipped validation.
/// </summary>
public sealed class InvalidGalleryCursorException()
    : Exception("The gallery cursor is malformed or belongs to a different sort.");