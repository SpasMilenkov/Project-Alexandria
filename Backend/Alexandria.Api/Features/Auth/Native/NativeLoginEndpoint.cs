using Alexandria.Api.Features.Auth.Login;
using Alexandria.Common.Services;
using Alexandria.Data.Models.Enumerators;
using Alexandria.Dto.Users;
using FastEndpoints;

namespace Alexandria.Api.Features.Auth.Native;

public sealed class NativeLoginEndpoint(IAuthService authService) : Endpoint<LoginRequest, NativeLoginResponse>
{
    public override void Configure()
    {
        Post("/auth/native/login");
        AllowAnonymous();
        Tags("Auth");
        Version(0);
        Summary(s =>
        {
            s.Summary = "Signs a native client in and returns its tokens in the body";
            s.Description = "Refused with 403 when the request comes from a browser; browsers use /auth/login.";
        });
    }

    public override async Task HandleAsync(LoginRequest req, CancellationToken ct)
    {
        // Checked before the credentials so a browser attempt neither counts toward lockout nor
        // shows up as a failed sign-in.
        if (BrowserRequests.IsFromBrowser(HttpContext.Request))
        {
            await Send.ForbiddenAsync(ct);
            return;
        }

        var result = await authService.AuthenticateAsync(req.Email, req.Password, ct);

        if (!result.Succeeded || result.User is null)
        {
            await Send.UnauthorizedAsync(ct);
            return;
        }

        var refreshToken = await authService.GenerateRefreshTokenAsync(result.User, SessionClient.Native, ct);
        var accessToken = authService.GenerateAccessToken(result.User, refreshToken.SessionId);

        await Send.OkAsync(new NativeLoginResponse
        {
            Success = true,
            User = new SessionUserDto
            {
                Id = result.User.Id,
                Email = result.User.Email ?? throw new InvalidOperationException(),
                Name = result.User.UserName ?? throw new InvalidOperationException(),
                AvatarUrl = AvatarPaths.Url(result.User.Id, result.User.AvatarId)
            },
            UserRoles = result.UserRoles,
            OnboardingStep = result.User.OnboardinStep,
            AccessToken = accessToken,
            RefreshToken = refreshToken.Token,
        }, ct);
    }
}