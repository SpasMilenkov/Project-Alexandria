using Alexandria.Common;
using Alexandria.Common.Exceptions;
using Alexandria.Common.Services;
using Alexandria.Dto.Users;
using Microsoft.Extensions.Logging;

namespace Alexandria.Services.User;

public partial class UserAvatarService(
    IStaticAssetService staticAssetService,
    IUnitOfWork unitOfWork,
    ILogger<UserAvatarService> logger)
    : IUserAvatarService
{
    public async Task<string> ReplaceAsync(Stream content, Guid userId, CancellationToken ct = default)
    {
        var avatarId = await staticAssetService.UploadAvatarAsync(content, userId, ct);

        if (!await unitOfWork.Users.SetAvatarAsync(userId, avatarId, ct))
        {
            await SweepAsync(userId, keepAvatarId: null, ct);
            throw new UserNotFoundException(userId);
        }

        await SweepAsync(userId, avatarId, ct);

        return AvatarPaths.Url(userId, avatarId)!;
    }

    public async Task RemoveAsync(Guid userId, CancellationToken ct = default)
    {
        if (!await unitOfWork.Users.SetAvatarAsync(userId, avatarId: null, ct))
            throw new UserNotFoundException(userId);

        await SweepAsync(userId, keepAvatarId: null, ct);
    }

    // Sweeping the whole prefix instead of deleting one remembered key also clears pictures
    // left behind by a failed delete or two uploads racing each other.
    private async Task SweepAsync(Guid userId, Guid? keepAvatarId, CancellationToken ct)
    {
        try
        {
            var removed = await staticAssetService.DeleteAvatarsExceptAsync(userId, keepAvatarId, ct);
            if (removed > 0)
                LogAvatarsSwept(logger, removed, userId);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // The account already points at the right picture; leftovers go on the next change.
            LogAvatarSweepFailed(logger, ex, userId);
        }
    }
}
