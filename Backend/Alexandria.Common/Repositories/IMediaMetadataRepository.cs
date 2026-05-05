using Alexandria.Data.Models;

namespace Alexandria.Common.Repositories;

public interface IMediaMetadataRepository : IRepository<MediaMetadata>
{
    Task<MediaMetadata> CreateAsync(MediaMetadata metadata, CancellationToken ct = default);
    Task<MediaMetadata> UpdateAsync(MediaMetadata metadata, CancellationToken ct = default);
}