using Common;
using DTO;
using FastEndpoints;
namespace API.Features.Auth.Login;

/** TODO Needs to be tested on multiple devices to see the behavior of the tokens,
 * possibly might want to invalidate previous tokens on relogin
 */
public class LoginEndpoint : Endpoint<LoginRequest, LoginResponse>
{
    private readonly IAuthService _authService;
    private readonly CsrfService _csrfService;

    public LoginEndpoint(IAuthService authService, CsrfService csrfService)
    {
        _authService = authService;
        _csrfService = csrfService;
    }

    public override void Configure()
    {
        Post("/api/auth/login");
        AllowAnonymous();
    }

    public override async Task HandleAsync(LoginRequest req, CancellationToken ct)
    {
        
        var result = await _authService.AuthenticateAsync(req.Email, req.Password);
        
        if (!result.Succeeded || result.User is null)
        {
            await Send.UnauthorizedAsync(ct);
            return;
        }
    
        var userId = result.User.Id.ToString();
        
        var accessToken = _authService.GenerateAccessToken(result.User);
        var refreshToken = await _authService.GenerateRefreshTokenAsync(result.User, ct);
        
        var (csrfCookie, csrfHeader) = _csrfService.GenerateToken(userId);

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

        HttpContext.Response.Cookies.Append("csrf_token", csrfCookie, new CookieOptions
        {
            HttpOnly = false,
            Secure = true,
            SameSite = SameSiteMode.Lax,
            MaxAge = TimeSpan.FromHours(1)
        });

        await Send.OkAsync(new LoginResponse
        {
            Success = true,
            User = new UserDto 
            { 
                Id = result.User.Id, 
                Email = result.User.Email ?? throw new InvalidOperationException(),
                Name = result.User.UserName ?? throw new InvalidOperationException()
            },
            CsrfToken = csrfHeader
        }, ct);
    }
}
