using System.Text.RegularExpressions;
using Alexandria.Common.Validation;
using Alexandria.Data.Models.Enumerators;
using Alexandria.Dto.Files.Streaming.Playlist;

namespace Alexandria.Services.Streaming.Playlists;

/// <summary>
/// Pure in-memory bucketing for auto-playlist grouping.
/// Artist values are split into individual names on common list separators before
/// normalization. Normalization is then exact per name: <c>Trim().ToLowerInvariant()</c>,
/// so "The Beatles" and "Beatles" stay split by design (no alias table, per spec
/// non-goals). Artist playlists bucket a file under every distinct parsed name; album
/// keys use the primary (first parsed) artist. Display names keep the first-seen trimmed
/// casing for later playlist naming. Active means non-suppressed on a live tag; the row
/// fetch projects everything and this helper filters, so suppression semantics have
/// exactly one implementation.
/// </summary>
internal static partial class AutoPlaylistGrouping
{
    private static readonly char[] ArtistListSeparators = [',', '、', ';'];

    [GeneratedRegex(@"\s+(?:feat\.?|ft\.?|featuring)\s+", RegexOptions.IgnoreCase)]
    private static partial Regex FeaturedArtistSeparator();

    private static string? Normalize(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim().ToLowerInvariant();
    }

    private static IReadOnlyList<string> SplitArtistNames(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return [];

        var names = new List<string>();
        var seen = new HashSet<string>(StringComparer.Ordinal);

        foreach (var segment in value.Split(
                     ArtistListSeparators,
                     StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            foreach (var part in FeaturedArtistSeparator().Split(segment))
            {
                var name = part.Trim();
                if (name.Length == 0)
                    continue;

                var key = Normalize(name);
                if (key is null || !seen.Add(key))
                    continue;

                names.Add(name);
            }
        }

        return names;
    }

    private static bool IsActive(GroupingTagRef tag)
    {
        if (tag.Source == TagSource.Suppressed)
            return false;
        if (tag.IsTagDeleted)
            return false;
        return true;
    }

    public static AutoPlaylistGroupingResult GroupAll(IEnumerable<AutoPlaylistGroupingRow> rows)
    {
        var materialized = rows.ToList();

        return new AutoPlaylistGroupingResult(
            GroupArtists(materialized),
            GroupAlbums(materialized),
            GroupTags(materialized),
            GroupGenreFallback(materialized),
            GroupParentGenres(materialized),
            GroupDecades(materialized));
    }

    public static IReadOnlyList<ArtistGroup> GroupArtists(IEnumerable<AutoPlaylistGroupingRow> rows)
    {
        var displayByKey = new Dictionary<string, string>(StringComparer.Ordinal);
        var filesByKey = new Dictionary<string, List<Guid>>(StringComparer.Ordinal);

        foreach (var row in rows)
        {
            foreach (var artist in SplitArtistNames(row.Artist))
            {
                var key = Normalize(artist);
                if (key is null)
                    continue;

                if (!displayByKey.ContainsKey(key))
                    displayByKey[key] = artist;

                if (!filesByKey.TryGetValue(key, out var files))
                {
                    files = [];
                    filesByKey[key] = files;
                }

                files.Add(row.FileId);
            }
        }

        return filesByKey
            .OrderBy(g => g.Key, StringComparer.Ordinal)
            .Select(g => new ArtistGroup(g.Key, displayByKey[g.Key], g.Value))
            .ToList();
    }

    public static IReadOnlyList<AlbumGroup> GroupAlbums(IEnumerable<AutoPlaylistGroupingRow> rows)
    {
        var displayByKey = new Dictionary<(string ArtistKey, string AlbumKey), (string Artist, string Album)>();
        var filesByKey = new Dictionary<(string ArtistKey, string AlbumKey), List<Guid>>();

        foreach (var row in rows)
        {
            var primaryArtist = SplitArtistNames(row.Artist).FirstOrDefault();
            var artistKey = Normalize(primaryArtist);
            var albumKey = Normalize(row.Album);
            if (artistKey is null || albumKey is null)
                continue;

            var key = (artistKey, albumKey);
            if (!displayByKey.ContainsKey(key))
                displayByKey[key] = (primaryArtist!, row.Album!.Trim());

            if (!filesByKey.TryGetValue(key, out var files))
            {
                files = [];
                filesByKey[key] = files;
            }

            files.Add(row.FileId);
        }

        return filesByKey
            .OrderBy(g => g.Key.ArtistKey, StringComparer.Ordinal)
            .ThenBy(g => g.Key.AlbumKey, StringComparer.Ordinal)
            .Select(g => new AlbumGroup(
                g.Key.ArtistKey,
                g.Key.AlbumKey,
                displayByKey[g.Key].Artist,
                displayByKey[g.Key].Album,
                g.Value))
            .ToList();
    }

    public static IReadOnlyList<TagGroup> GroupTags(IEnumerable<AutoPlaylistGroupingRow> rows)
    {
        var facetByTag = new Dictionary<Guid, TagFacet?>();
        var filesByTag = new Dictionary<Guid, List<Guid>>();

        foreach (var row in rows)
        {
            foreach (var tag in row.Tags)
            {
                if (!IsActive(tag))
                    continue;
                if (!facetByTag.ContainsKey(tag.TagId))
                    facetByTag[tag.TagId] = tag.Facet;
                if (!filesByTag.TryGetValue(tag.TagId, out var files))
                {
                    files = [];
                    filesByTag[tag.TagId] = files;
                }

                files.Add(row.FileId);
            }
        }

        return filesByTag
            .OrderBy(g => g.Key)
            .Select(g => new TagGroup(g.Key, facetByTag[g.Key], g.Value))
            .ToList();
    }

    public static IReadOnlyList<GenreFallbackGroup> GroupGenreFallback(IEnumerable<AutoPlaylistGroupingRow> rows)
    {
        var displayByKey = new Dictionary<string, string>(StringComparer.Ordinal);
        var filesByKey = new Dictionary<string, List<Guid>>(StringComparer.Ordinal);

        foreach (var row in rows)
        {
            if (row.Tags.Any(IsActive))
                continue;
            var key = Normalize(row.Genre);
            if (key is null)
                continue;
            if (!displayByKey.ContainsKey(key))
                displayByKey[key] = row.Genre!.Trim();
            if (!filesByKey.TryGetValue(key, out var files))
            {
                files = [];
                filesByKey[key] = files;
            }

            files.Add(row.FileId);
        }

        return filesByKey
            .OrderBy(g => g.Key, StringComparer.Ordinal)
            .Select(g => new GenreFallbackGroup(g.Key, displayByKey[g.Key], g.Value))
            .ToList();
    }

    public static IReadOnlyList<ParentGenreGroup> GroupParentGenres(
        IEnumerable<AutoPlaylistGroupingRow> rows)
    {
        var displayByParent = new Dictionary<Guid, string>();
        var filesByParent = new Dictionary<Guid, List<Guid>>();
        var seenByParent = new Dictionary<Guid, HashSet<Guid>>();

        foreach (var row in rows)
        {
            foreach (var tag in row.Tags)
            {
                if (!IsActive(tag))
                    continue;
                if (tag.Facet != TagFacet.Genre)
                    continue;

                var parentId = tag.ParentTagId ?? tag.TagId;
                if (!displayByParent.ContainsKey(parentId) && !string.IsNullOrWhiteSpace(tag.ParentName))
                    displayByParent[parentId] = tag.ParentName!.Trim();
                if (!filesByParent.TryGetValue(parentId, out var files))
                {
                    files = [];
                    filesByParent[parentId] = files;
                    seenByParent[parentId] = [];
                }

                if (seenByParent[parentId].Add(row.FileId))
                    files.Add(row.FileId);
            }
        }

        return filesByParent
            .OrderBy(g => displayByParent.GetValueOrDefault(g.Key, g.Key.ToString()), StringComparer.Ordinal)
            .Select(g => new ParentGenreGroup(
                g.Key, displayByParent.GetValueOrDefault(g.Key, string.Empty), g.Value))
            .ToList();
    }

    public static IReadOnlyList<DecadeGroup> GroupDecades(IEnumerable<AutoPlaylistGroupingRow> rows)
    {
        var filesByDecade = new Dictionary<int, List<Guid>>();

        foreach (var row in rows)
        {
            var decade = ToDecade(row.Year);
            if (decade is null)
                continue;
            if (!filesByDecade.TryGetValue(decade.Value, out var files))
            {
                files = [];
                filesByDecade[decade.Value] = files;
            }

            files.Add(row.FileId);
        }

        return filesByDecade
            .OrderBy(g => g.Key)
            .Select(g => new DecadeGroup(g.Key, g.Value))
            .ToList();
    }

    private static int? ToDecade(string? year)
    {
        if (!MetadataValidation.IsValidYear(year?.Trim()))
            return null;
        return int.Parse(year!.Trim()) / 10 * 10;
    }
}