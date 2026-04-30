using Alexandria.Api.Features.Auth.Extensions;
using Alexandria.Common.Services;
using FastEndpoints;

namespace Alexandria.Api.Features.Users.FinishTour;

sealed class FinishTourEndpoint(IUserManagementService userManagementService) : EndpointWithoutRequest
{
    public override void Configure()
    {
        Patch("/users/finish-tour");
        Policies(Common.Auth.Policies.RequireUser);
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var userId = User.GetUserId();

        await userManagementService.FinishTourAsync(userId, ct);

        await Send.OkAsync(cancellation: ct);
    }
}