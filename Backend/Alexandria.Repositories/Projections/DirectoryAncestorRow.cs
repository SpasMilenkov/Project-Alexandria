namespace Alexandria.Repositories.Projections;

/// <summary>A folder on the way up from a starting folder; depth 0 is the starting folder itself.</summary>
public sealed record DirectoryAncestorRow(Guid Id, int Depth);
