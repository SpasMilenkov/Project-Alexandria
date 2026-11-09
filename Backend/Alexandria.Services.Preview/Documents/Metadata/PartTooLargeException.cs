namespace Alexandria.Services.Preview.Documents.Metadata;

/// <summary>
/// Thrown when a package part inflates past its read limit.
/// </summary>
internal sealed class PartTooLargeException(string partName, long limit)
    : Exception($"Package part '{partName}' is larger than {limit} bytes.");
