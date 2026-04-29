using Alexandria.Api.Features.Auth.Extensions;
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
        var userId = User.GetUserId();

        await userManagementService.GetUserProfile(userId, ct);

        await Send.OkAsync(await userManagementService.GetUserProfile(userId, ct), cancellation: ct);
    }
}