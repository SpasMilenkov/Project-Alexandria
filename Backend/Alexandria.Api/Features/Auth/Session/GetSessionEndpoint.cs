using Alexandria.Api.Features.Auth.Extensions;
using Alexandria.Common.Services;
using Alexandria.Dto.Users;
using FastEndpoints;

namespace Alexandria.Api.Features.Auth.Session;

public sealed class GetSessionEndpoint(IAuthService authService)
    : EndpointWithoutRequest<SessionResponse>
{
    public override void Configure()
    {
        Get("/auth/session");
        Policies(Common.Auth.Policies.RequireUser);
        Tags("Auth");
        Version(0);
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        HttpContext.Response.Headers.CacheControl = "no-store";

        var result = await authService.GetSessionAsync(User.GetUserId(), ct);

        if (!result.Succeeded || result.User is null)
        {
            await Send.UnauthorizedAsync(ct);
            return;
        }

        await Send.OkAsync(new SessionResponse
        {
            User = new SessionUserDto
            {
                Id = result.User.Id,
                Email = result.User.Email ?? throw new InvalidOperationException(),
                Name = result.User.UserName ?? throw new InvalidOperationException(),
                AvatarUrl = AvatarPaths.Url(result.User.Id, result.User.AvatarId)
            },
            UserRoles = result.UserRoles,
            OnboardingStep = result.User.OnboardinStep
        }, ct);
    }
}
