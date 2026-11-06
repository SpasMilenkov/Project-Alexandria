namespace Alexandria.Common.Exceptions;

/// <summary>
/// Thrown when a worker gave up waiting for an uploaded file to reach permanent storage.
/// </summary>
public class ContentNotPromotedException(Guid contentObjectId, TimeSpan limit)
    : Exception($"Content object '{contentObjectId}' was not promoted to permanent storage within {limit}.")
{
    public Guid ContentObjectId { get; } = contentObjectId;
}
