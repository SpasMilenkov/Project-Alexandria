namespace Alexandria.Common.Exceptions.Transpilation;

public class TranspilationCancelledException(Guid id)
    : Exception($"Transpilation job with {id} has been stopped.");