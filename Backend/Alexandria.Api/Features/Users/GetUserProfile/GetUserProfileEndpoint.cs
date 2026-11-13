using Alexandria.Api.Features.Auth.Extensions;
using Alexandria.Common.Exceptions;
using Alexandria.Common.Services;
using Alexandria.Dto.Users;
using FastEndpoints;

namespace Alexandria.Api.Features.Users.GetUserProfile;

sealed class GetUserProfileEndpoint(IUserManagementService userManagementService)
    : EndpointWithoutRequest<UserProfileDto>
{
    public override void Configure()
    {
        Get("/users/profile");
        Policies(Common.Auth.Policies.RequireUser);
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        try
        {
            var profile = await userManagementService.GetUserProfileAsync(User.GetUserId(), ct);
            await Send.OkAsync(profile, cancellation: ct);
        }
        catch (UserNotFoundException)
        {
            await Send.NotFoundAsync(ct);
        }
    }
}
