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
        HttpContext.Request.Cookies.TryGetValue(AuthCookies.RefreshToken, out var currentToken);

        var sessions = await authService.GetSessionsAsync(User.GetUserId(), currentToken, ct);
        await Send.OkAsync(sessions, ct);
    }
}
