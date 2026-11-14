namespace Alexandria.Data.Context.Functions;

/// <summary>
/// SQL functions created by migrations and mapped in <see cref="AlexandriaDbContext"/>, so LINQ
/// projections can call them. They only work inside a query; calling one in memory throws.
/// </summary>
public static class DatabaseFunctions
{
    /// <summary>
    /// Bytes of every live file under the folder, its live subfolders included. A trashed folder
    /// reports zero.
    /// </summary>
    public static long DirectoryTreeSize(Guid directoryId) =>
        throw new NotSupportedException($"{nameof(DirectoryTreeSize)} only runs inside a database query.");
}