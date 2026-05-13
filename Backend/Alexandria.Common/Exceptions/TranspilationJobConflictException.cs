namespace Alexandria.Common.Exceptions;

public class TranspilationJobConflictException(Guid contentObjectId, Guid existingJobId)
    : Exception($"Content object '{contentObjectId}' already has an active transpilation job '{existingJobId}'.")
{
    public Guid ContentObjectId { get; } = contentObjectId;
    public Guid ExistingJobId { get; } = existingJobId;
}