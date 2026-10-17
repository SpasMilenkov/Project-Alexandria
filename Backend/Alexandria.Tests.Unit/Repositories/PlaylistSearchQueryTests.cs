using Alexandria.Data.Context;
using Alexandria.Repositories;
using AwesomeAssertions;
using Microsoft.EntityFrameworkCore;

namespace Alexandria.Tests.Unit.Repositories;

public class PlaylistSearchQueryTests
{
    private static AlexandriaDbContext TranslationContext()
    {
        var options = new DbContextOptionsBuilder<AlexandriaDbContext>()
            .UseNpgsql("Host=localhost;Database=translation_only;Username=postgres;Password=postgres")
            .Options;
        return new AlexandriaDbContext(options);
    }

    [Fact]
    public void ApplySearchFilter_meta_scope_translates_to_name_and_description_ilike()
    {
        using var context = TranslationContext();

        var sql = PlaylistRepository
            .ApplySearchFilter(context.Playlists, "beatles", searchItems: false)
            .ToQueryString();

        sql.Should().Contain("\"Name\" ILIKE");
        sql.Should().Contain("\"Description\" ILIKE");
        sql.Should().NotContain("\"PlaylistItems\"");
    }

    [Fact]
    public void ApplySearchFilter_item_scope_translates_to_an_item_exists_subquery()
    {
        using var context = TranslationContext();

        var sql = PlaylistRepository
            .ApplySearchFilter(context.Playlists, "beatles", searchItems: true)
            .ToQueryString();

        sql.Should().Contain("\"PlaylistItems\"");
        sql.Should().Contain("\"Title\" ILIKE");
        sql.Should().Contain("\"Name\" ILIKE");
    }
}