using Alexandria.Data.Models.Enumerators;

namespace Alexandria.Dto.Files.Streaming.Playlist;

/// <summary>
/// One tag assignment on a grouping row. The row fetch projects every assignment
/// unfiltered; is the single authority deciding which ones are active.
/// </summary>
public sealed record GroupingTagRef(
    Guid TagId,
    TagSource Source,
    TagFacet? Facet,
    bool IsTagDeleted,
    Guid? ParentTagId,
    string? ParentName);

/// <summary>
/// Owner-scoped fetch-once input for auto-playlist grouping. One row per live file;
/// bucketing happens in memory over these rows, never in further queries.
/// </summary>
public sealed record AutoPlaylistGroupingRow(
    Guid FileId,
    string? Artist,
    string? Album,
    string? Genre,
    string? Year,
    IReadOnlyList<GroupingTagRef> Tags);

/// <summary>Files sharing one normalized artist key (D4: exact normalized match).</summary>
public sealed record ArtistGroup(
    string Key,
    string DisplayName,
    IReadOnlyList<Guid> FileIds);

/// <summary>
/// Files sharing one Artist+Album composite key. Album alone is never a key (D4),
/// and rows without an artist are skipped rather than bucketed under an empty key.
/// </summary>
public sealed record AlbumGroup(
    string ArtistKey,
    string AlbumKey,
    string DisplayArtist,
    string DisplayAlbum,
    IReadOnlyList<Guid> FileIds);

/// <summary>
/// Files sharing one tag. Facet is carried from the tag for later phase filtering;
/// there are no per-facet rollup buckets (the spec has no Facet grouping kind).
/// </summary>
public sealed record TagGroup(
    Guid TagId,
    TagFacet? Facet,
    IReadOnlyList<Guid> FileIds);

/// <summary>
/// Files grouped by free-text genre, only when they carry no active tags (D6).
/// Files with any active tag are covered by tag buckets instead.
/// </summary>
public sealed record GenreFallbackGroup(
    string Key,
    string DisplayName,
    IReadOnlyList<Guid> FileIds);

/// <summary>
/// Files sharing one parent genre: union of every child subgenre bucket plus files
/// tagged with the parent itself. Replaces per-subgenre playlists (D17).
/// </summary>
public sealed record ParentGenreGroup(
    Guid ParentTagId,
    string DisplayName,
    IReadOnlyList<Guid> FileIds);

/// <summary>
/// Files released in one strict calendar decade (key is the decade start year,
/// e.g. 2010). Only valid 4-digit years bucket; everything else drops out.
/// </summary>
public sealed record DecadeGroup(
    int Decade,
    IReadOnlyList<Guid> FileIds);

/// <summary>
/// All grouping views over one fetch. Buckets are independent: a file appears in
/// every bucket it matches (artist + album + each of its tags, or genre fallback).
/// </summary>
public sealed record AutoPlaylistGroupingResult(
    IReadOnlyList<ArtistGroup> Artists,
    IReadOnlyList<AlbumGroup> Albums,
    IReadOnlyList<TagGroup> Tags,
    IReadOnlyList<GenreFallbackGroup> GenreFallback,
    IReadOnlyList<ParentGenreGroup> ParentGenres,
    IReadOnlyList<DecadeGroup> Decades);