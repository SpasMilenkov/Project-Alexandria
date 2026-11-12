using Alexandria.Api.Features.Auth.Extensions;
using Alexandria.Common.Services;
using Alexandria.Dto.Files;
using FastEndpoints;

namespace Alexandria.Api.Features.Auth.Login;

public class LoginEndpoint : Endpoint<LoginRequest, LoginResponse>
{
    private readonly IAuthService _authService;

    public LoginEndpoint(IAuthService authService)
    {
        _authService = authService;
    }

    public override void Configure()
    {
        Post("/auth/login");
        AllowAnonymous();
        Tags("Auth");
        Version(0);
    }

    public override async Task HandleAsync(LoginRequest req, CancellationToken ct)
    {
        var result = await _authService.AuthenticateAsync(req.Email, req.Password, ct);

        if (!result.Succeeded || result.User is null)
        {
            await Send.UnauthorizedAsync(ct);
            return;
        }

        var accessToken = _authService.GenerateAccessToken(result.User);
        var refreshToken = await _authService.GenerateRefreshTokenAsync(result.User, ct);

        HttpContext.Response.AppendSessionCookies(accessToken, refreshToken);

        await Send.OkAsync(new LoginResponse
        {
            Success = true,
            User = new UserDto
            {
                Id = result.User.Id,
                Email = result.User.Email ?? throw new InvalidOperationException(),
                Name = result.User.UserName ?? throw new InvalidOperationException()
            },
            UserRoles = result.UserRoles,
            OnboardingStep = result.User.OnboardinStep
        }, ct);
    }
}