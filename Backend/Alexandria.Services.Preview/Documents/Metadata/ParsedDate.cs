namespace Alexandria.Services.Preview.Documents.Metadata;

/// <summary>
/// A date read from a document. A true instant when <see cref="OffsetMinutes"/> is set,
/// otherwise the author's wall clock.
/// </summary>
internal readonly record struct ParsedDate(DateTime Value, short? OffsetMinutes);
