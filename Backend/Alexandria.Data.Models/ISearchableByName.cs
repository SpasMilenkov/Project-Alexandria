using NpgsqlTypes;

namespace Alexandria.Data.Models;

/// <summary>
/// An entity searched by its name through the stored normalized name and text-search
/// vector, so file and directory search can share one matching and ordering query.
/// </summary>
public interface ISearchableByName
{
    Guid Id { get; }
    string Name { get; }
    string NormalizedName { get; }
    NpgsqlTsVector SearchVector { get; }
}
