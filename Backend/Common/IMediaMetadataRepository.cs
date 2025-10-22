using Models;

namespace Common;

public interface IMediaMetadataRepository : IRepository<MediaMetadata>
{
    Task<MediaMetadata> CreateAsync(MediaMetadata metadata, CancellationToken ct = default);
    Task<MediaMetadata> UpdateAsync(MediaMetadata metadata, CancellationToken ct = default);
}