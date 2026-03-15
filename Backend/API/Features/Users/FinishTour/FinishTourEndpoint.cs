using API.Features.Auth.Extensions;
using Common.Services;
using FastEndpoints;

namespace API.Features.Users.FinishTour;

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

        await userManagementService.FinishTour(userId, ct);

        await Send.OkAsync(cancellation: ct);
    }
}
