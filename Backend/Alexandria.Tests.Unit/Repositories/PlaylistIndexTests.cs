using System.Text.RegularExpressions;
using Alexandria.Data.Configurations;
using Alexandria.Data.Models;
using Alexandria.Data.Models.Enumerators;
using AwesomeAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace Alexandria.Tests.Unit.Repositories;

public class PlaylistIndexTests
{
    private static List<IMutableIndex> AutoGroupIndexes()
    {
        var builder = new ModelBuilder();
        builder.ApplyConfiguration(new PlaylistConfiguration());
        var entity = builder.Model.FindEntityType(typeof(Playlist))!;
        return entity.GetIndexes()
            .Where(i => i.IsUnique && (i.GetFilter() ?? "").Contains("AutoGroupKind"))
            .ToList();
    }

    [Fact]
    public void AutoGroup_indexes_cover_all_kinds()
    {
        var indexes = AutoGroupIndexes();

        indexes.Should().HaveCount(5);
        indexes.SelectMany(i => KindsIn(i.GetFilter()!)).Should()
            .BeEquivalentTo([0, 1, 2, 3, 4, 5]);
    }

    private static IEnumerable<int> KindsIn(string filter)
    {
        return Regex.Matches(filter, @"\d+")
            .Select(m => int.Parse(m.Value));
    }

    [Fact]
    public void AutoGroup_indexes_exclude_soft_deleted_rows()
    {
        var indexes = AutoGroupIndexes();

        foreach (var index in indexes)
            index.GetFilter().Should().Contain("\"DeletedAt\" IS NULL");
    }

    [Fact]
    public void Tag_kinds_share_one_index_covering_both()
    {
        var names = AutoGroupIndexes().Select(i => i.GetDatabaseName()).ToList();

        names.Should().HaveCount(names.Distinct().Count());
        var shared = AutoGroupIndexes()
            .Single(i => i.GetDatabaseName() == "IX_Playlists_OwnerId_AutoGroupKind_AutoTagId");
        shared.GetFilter().Should().Contain(((int)AutoGroupKind.Tag).ToString());
        shared.GetFilter().Should().Contain(((int)AutoGroupKind.ParentGenre).ToString());
    }
}