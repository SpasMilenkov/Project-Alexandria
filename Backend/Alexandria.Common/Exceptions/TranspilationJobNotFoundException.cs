namespace Alexandria.Common.Exceptions;

public class TranspilationJobNotFoundException(Guid id)
    : Exception($"Transpilation job '{id}' was not found.");