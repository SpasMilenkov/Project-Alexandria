using API.Features.Auth.Extensions;
using Common.Services;
using DTO.Users;
using FastEndpoints;

namespace API.Features.Users.GetUserProfile;

sealed class GetUserProfileEndpoint(IUserManagementService userManagementService) : EndpointWithoutRequest<UserProfileDto>
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
