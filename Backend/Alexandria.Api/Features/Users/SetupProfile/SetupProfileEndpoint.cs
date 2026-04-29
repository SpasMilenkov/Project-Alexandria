using Alexandria.Api.Features.Auth.Extensions;
using Alexandria.Common.Services;
using FastEndpoints;

namespace Alexandria.Api.Features.Users.SetupProfile;

sealed class SetupProfileEndpoint(IUserManagementService userManagementService) : EndpointWithoutRequest
{
    public override void Configure()
    {
        Patch("/users/setup-profile");
        Policies(Common.Auth.Policies.RequireUser);
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var userId = User.GetUserId();

        await userManagementService.SetupProfile(userId, ct);

        await Send.OkAsync(cancellation: ct);
    }
}