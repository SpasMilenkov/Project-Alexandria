using System.Security.Claims;
using API.Features.Auth;

namespace API.Middlewares;

public class CsrfMiddleware
{
    private readonly RequestDelegate _next;
    private readonly CsrfService _csrfService;
    private static readonly HashSet<string> SafeMethods = new() { "GET", "HEAD", "OPTIONS", "TRACE" };

    public CsrfMiddleware(RequestDelegate next, CsrfService csrfService)
    {
        _next = next;
        _csrfService = csrfService;
    }

    public async Task InvokeAsync(HttpContext ctx)
    {
        // Skip CSRF validation for safe methods (GET, HEAD, OPTIONS, TRACE)
        if (SafeMethods.Contains(ctx.Request.Method))
        {
            await _next(ctx);
            return;
        }

        if (!ctx.User.Identity?.IsAuthenticated ?? true)
        {
            await _next(ctx);
            return;
        }

        var csrfCookie = ctx.Request.Cookies["csrf_token"];
        var csrfHeader = ctx.Request.Headers["X-CSRF-Token"].FirstOrDefault();

        var userId = ctx.User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (userId == null)
        {
            ctx.Response.StatusCode = 401;
            await ctx.Response.WriteAsJsonAsync(new { error = "Invalid authentication" });
            return;
        }

        if (!_csrfService.ValidateToken(csrfCookie, csrfHeader, userId))
        {
            ctx.Response.StatusCode = 403;
            await ctx.Response.WriteAsJsonAsync(new { error = "Invalid or expired CSRF token" });
            return;
        }

        await _next(ctx);
    }
}