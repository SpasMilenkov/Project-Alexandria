using Alexandria.Common.Repositories;
using Alexandria.Common.Services;
using Alexandria.Dto.Files.Streaming.Playlist;

namespace Alexandria.Services.Streaming;

public sealed class AutoPlaylistGroupingService(IFileRepository files) : IAutoPlaylistGroupingService
{
    public async Task<AutoPlaylistGroupingResult> GetGroupingAsync(Guid ownerId, CancellationToken ct = default)
    {
        var rows = await files.GetAutoPlaylistGroupingRowsAsync(ownerId, ct);
        return AutoPlaylistGrouping.GroupAll(rows);
    }
}