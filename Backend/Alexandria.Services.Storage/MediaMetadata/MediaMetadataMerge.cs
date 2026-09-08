using Alexandria.Dto.Files;
using MediaMetadataEntity = Alexandria.Data.Models.MediaMetadata;

namespace Alexandria.Services.Storage.MediaMetadata;

/// <summary>
/// Pure merge rules for the descriptive <see cref="MediaMetadataEntity"/> fields
/// (Title, Artist, Album, Year, Genre). Per-field preserve-if-set: a stored
/// non-empty value always wins over incoming automated output (this is what keeps
/// user corrections alive across Media worker re-runs); an empty stored value takes
/// the incoming one. Technical fields are never handled here.
/// </summary>
internal static class MediaMetadataMerge
{
    public static void ApplyDescriptiveFields(MediaMetadataEntity existing, MediaMetadataDto incoming)
    {
        if (string.IsNullOrWhiteSpace(existing.Title))
            existing.Title = incoming.Title;

        if (string.IsNullOrWhiteSpace(existing.Artist))
            existing.Artist = incoming.Artist;

        if (string.IsNullOrWhiteSpace(existing.Album))
            existing.Album = incoming.Album;

        if (string.IsNullOrWhiteSpace(existing.Year))
            existing.Year = incoming.Year;

        if (string.IsNullOrWhiteSpace(existing.Genre))
            existing.Genre = incoming.Genre;
    }
}