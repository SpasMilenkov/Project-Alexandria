using Alexandria.Common.Services;
using Alexandria.Data.Models.Enumerators;
using Alexandria.Dto.Audit;
using Alexandria.Dto.Files;
using FastEndpoints;

namespace Alexandria.Api.Features.Auth.Login;

public class LoginEndpoint : Endpoint<LoginRequest, LoginResponse>
{
    private readonly IAuthService _authService;
    private readonly IAuditService _auditService;

    public LoginEndpoint(IAuthService authService, IAuditService auditService)
    {
        _authService = authService;
        _auditService = auditService;
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
        var result = await _authService.AuthenticateAsync(req.Email, req.Password);

        if (!result.Succeeded || result.User is null)
        {
            await Send.UnauthorizedAsync(ct);
            return;
        }

        var accessToken = _authService.GenerateAccessToken(result.User);
        var refreshToken = await _authService.GenerateRefreshTokenAsync(result.User, ct);

        HttpContext.Response.Cookies.Append("access_token", accessToken, new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.Lax,
            MaxAge = TimeSpan.FromMinutes(15)
        });

        HttpContext.Response.Cookies.Append("refresh_token", refreshToken, new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.Lax,
            MaxAge = TimeSpan.FromDays(7),
            Path = "/api/auth/refresh"
        });

        await _auditService.LogAsync(
            new AuditLogDto(
                OperationType.Login,
                EntityType.User,
                result.User.Id,
                result.User.Id,
                AuditEventCode.UserLogin,
                null,
                HttpContext.Connection.RemoteIpAddress?.ToString() ?? ""
            ));

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