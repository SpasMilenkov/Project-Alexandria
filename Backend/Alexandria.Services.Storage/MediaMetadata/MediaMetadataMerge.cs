using Alexandria.Dto.Files;
using MediaMetadataEntity = Alexandria.Data.Models.MediaMetadata;

namespace Alexandria.Services.Storage.MediaMetadata;

/// <summary>
/// Pure merge rules for the descriptive <see cref="MediaMetadataEntity"/> fields
/// (Title, Artist, Album, Year, Genre). Per-field preserve-if-set: a stored
/// non-empty value always wins over incoming automated output (this is what keeps
/// user corrections alive across Media worker re-runs); an empty stored value takes
/// the incoming one. When <paramref name="allowOverwrite"/> is true, incoming output
/// always wins. Technical fields are never handled here.
/// </summary>
internal static class MediaMetadataMerge
{
    public static void ApplyDescriptiveFields(
        MediaMetadataEntity existing,
        MediaMetadataDto incoming,
        bool allowOverwrite = false)
    {
        if (allowOverwrite || string.IsNullOrWhiteSpace(existing.Title))
            existing.Title = incoming.Title;

        if (allowOverwrite || string.IsNullOrWhiteSpace(existing.Artist))
            existing.Artist = incoming.Artist;

        if (allowOverwrite || string.IsNullOrWhiteSpace(existing.Album))
            existing.Album = incoming.Album;

        if (allowOverwrite || string.IsNullOrWhiteSpace(existing.Year))
            existing.Year = incoming.Year;

        if (allowOverwrite || string.IsNullOrWhiteSpace(existing.Genre))
            existing.Genre = incoming.Genre;
    }
}