using Alexandria.Api.Features.Auth.Extensions;
using Alexandria.Common.Exceptions;
using Alexandria.Common.Services;
using FastEndpoints;

namespace Alexandria.Api.Features.Users.DeleteAvatar;

sealed class DeleteAvatarEndpoint(IUserAvatarService avatarService) : EndpointWithoutRequest
{
    public override void Configure()
    {
        Delete("/users/profile/avatar");
        Policies(Common.Auth.Policies.RequireUser);
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        try
        {
            await avatarService.RemoveAsync(User.GetUserId(), ct);
            await Send.NoContentAsync(ct);
        }
        catch (UserNotFoundException)
        {
            await Send.NotFoundAsync(ct);
        }
    }
}
