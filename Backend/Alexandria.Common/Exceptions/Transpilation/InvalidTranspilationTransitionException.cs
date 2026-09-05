using Alexandria.Data.Models.Enumerators;

namespace Alexandria.Common.Exceptions.Transpilation;

public class InvalidTranspilationTransitionException(
    JobStatus status,
    JobStatus statusTarget,
    Guid id)
    : Exception($"Status transition from {status} to {statusTarget} failed for job with ID: {id}.");