using Alexandria.Api.Features.Auth.Extensions;
using Alexandria.Common.Services;
using Alexandria.Dto.Sessions;
using FastEndpoints;
using Microsoft.AspNetCore.Mvc;

namespace Alexandria.Api.Features.Auth.Sessions;

sealed class GetSessionsEndpoint(IAuthService authService) : EndpointWithoutRequest<IReadOnlyList<UserSessionDto>>
{
    public override void Configure()
    {
        Get("/auth/sessions");
        Policies(Common.Auth.Policies.RequireUser);
        ResponseCache(0, ResponseCacheLocation.None, noStore: true);
        Summary(s => s.Summary = "Devices and browsers currently signed in to the caller's account");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        // The access token names its session, so a native client, which has no cookie, sees its
        // own session marked as well as a browser does.
        var sessions = await authService.GetSessionsAsync(User.GetUserId(), User.GetSessionId(), ct);
        await Send.OkAsync(sessions, ct);
    }
}