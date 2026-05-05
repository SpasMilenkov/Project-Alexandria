using Alexandria.Data.Models;

namespace Alexandria.Dto.Extensions;

public static class MediaMetadataExtensions
{
    public static MediaMetadata ToEntity(
        this Files.MediaMetadata dto,
        Guid fileId,
        string? thumbnailPath = null)
    {
        return new MediaMetadata
        {
            Id = Guid.NewGuid(),
            FileId = fileId,
            Duration = dto.Duration,
            BitrateMbps = dto.BitrateMbps,
            FormatName = dto.FormatName,
            ThumbnailPath = thumbnailPath,
            VideoCodec = dto.VideoCodec,
            AudioCodec = dto.AudioCodec,
            Width = dto.Width,
            Height = dto.Height,
            HasAudio = dto.HasAudio,
            Title = dto.Title,
            Artist = dto.Artist,
            Album = dto.Album,
            Year = dto.Year,
            Genre = dto.Genre,
            CreatedAt = DateTime.UtcNow
        };
    }
}