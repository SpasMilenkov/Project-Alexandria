using Alexandria.Data.Context;
using Alexandria.Data.Models.Enumerators;
using Alexandria.Repositories;
using AwesomeAssertions;
using Microsoft.EntityFrameworkCore;

namespace Alexandria.Tests.Unit.Repositories;

public class PlaylistSortQueryTests
{
    private static AlexandriaDbContext TranslationContext()
    {
        var options = new DbContextOptionsBuilder<AlexandriaDbContext>()
            .UseNpgsql("Host=localhost;Database=translation_only;Username=postgres;Password=postgres")
            .Options;
        return new AlexandriaDbContext(options);
    }

    [Fact]
    public void ApplySort_name_ascending_orders_by_name()
    {
        using var context = TranslationContext();

        var sql = PlaylistRepository
            .ApplySort(context.Playlists, PlaylistSort.Name, SortDirection.Asc)
            .ToQueryString();

        sql.Should().Contain("ORDER BY p.\"Name\", p.\"CreatedAt\" DESC");
    }

    [Fact]
    public void ApplySort_name_descending_flips_the_primary_key()
    {
        using var context = TranslationContext();

        var sql = PlaylistRepository
            .ApplySort(context.Playlists, PlaylistSort.Name, SortDirection.Desc)
            .ToQueryString();

        sql.Should().Contain("ORDER BY p.\"Name\" DESC, p.\"CreatedAt\" DESC");
    }

    [Fact]
    public void ApplySort_newest_ascending_orders_by_created_at()
    {
        using var context = TranslationContext();

        var sql = PlaylistRepository
            .ApplySort(context.Playlists, PlaylistSort.Newest, SortDirection.Asc)
            .ToQueryString();

        sql.Should().Contain("ORDER BY p.\"CreatedAt\"");
        sql.Should().NotContain("\"CreatedAt\" DESC");
    }

    [Fact]
    public void ApplySort_newest_descending_orders_by_created_at_descending()
    {
        using var context = TranslationContext();

        var sql = PlaylistRepository
            .ApplySort(context.Playlists, PlaylistSort.Newest, SortDirection.Desc)
            .ToQueryString();

        sql.Should().Contain("ORDER BY p.\"CreatedAt\" DESC");
    }

    [Fact]
    public void ApplySort_track_count_ascending_orders_by_item_count()
    {
        using var context = TranslationContext();

        var sql = PlaylistRepository
            .ApplySort(context.Playlists, PlaylistSort.TrackCount, SortDirection.Asc)
            .ToQueryString();

        sql.Should().Contain("count(*)::int");
        sql.Should().Contain("\"DeletedAt\" IS NULL), p.\"Name\"");
    }

    [Fact]
    public void ApplySort_track_count_descending_flips_the_primary_key()
    {
        using var context = TranslationContext();

        var sql = PlaylistRepository
            .ApplySort(context.Playlists, PlaylistSort.TrackCount, SortDirection.Desc)
            .ToQueryString();

        sql.Should().Contain("count(*)::int");
        sql.Should().Contain("\"DeletedAt\" IS NULL) DESC, p.\"Name\"");
    }
}