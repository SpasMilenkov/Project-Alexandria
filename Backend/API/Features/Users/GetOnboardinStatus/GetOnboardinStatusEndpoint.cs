using API.Features.Auth.Extensions;
using Common.Services;
using FastEndpoints;
using Models.Enumerators;

namespace API.Features.Users.GetOnboardinStatus;

internal sealed class GetOnboardingStatusResponse
{
    public OnboardingStep OnboardingStep { get; set; }
}

internal sealed class GetOnboardingStatusEndpoint(IUserManagementService userManagementService) : EndpointWithoutRequest<GetOnboardingStatusResponse>
{
    public override void Configure()
    {
        Get("users/onboarding");
        Policies(Common.Auth.Policies.RequireUser);
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var userId = User.GetUserId();

        var step = await userManagementService.GetOnboardingStep(userId, ct);
        if (step is null)
        {
            await Send.NotFoundAsync(cancellation: ct);
            return;
        }
        
        await Send.OkAsync(new GetOnboardingStatusResponse { OnboardingStep = step ?? OnboardingStep.Tour }, cancellation: ct);
    }
}
