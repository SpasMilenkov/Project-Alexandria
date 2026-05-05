using Alexandria.Api.Features.Auth.Extensions;
using Alexandria.Common.Services;
using Alexandria.Data.Models.Enumerators;
using FastEndpoints;

namespace Alexandria.Api.Features.Users.GetOnboardinStatus;

internal sealed class GetOnboardingStatusResponse
{
    public OnboardingStep OnboardingStep { get; set; }
}

internal sealed class GetOnboardingStatusEndpoint(IUserManagementService userManagementService)
    : EndpointWithoutRequest<GetOnboardingStatusResponse>
{
    public override void Configure()
    {
        Get("users/onboarding");
        Policies(Common.Auth.Policies.RequireUser);
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var userId = User.GetUserId();

        var step = await userManagementService.GetOnboardingStepAsync(userId, ct);
        if (step is null)
        {
            await Send.NotFoundAsync(cancellation: ct);
            return;
        }

        await Send.OkAsync(new GetOnboardingStatusResponse { OnboardingStep = step ?? OnboardingStep.Tour },
            cancellation: ct);
    }
}