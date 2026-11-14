using Microsoft.EntityFrameworkCore;

namespace Alexandria.Data.Context.Functions;

/// <summary>
/// Ties each <see cref="DatabaseFunctions"/> stub to the SQL function its migration creates. A
/// rename on either side only fails at query time, so the integration tests that call these are
/// what keep them in step.
/// </summary>
public static class DatabaseFunctionMappings
{
    public static ModelBuilder MapDatabaseFunctions(this ModelBuilder builder)
    {
        builder.HasDbFunction(() => DatabaseFunctions.DirectoryTreeSize(default))
            .HasName("directory_tree_size");

        return builder;
    }
}