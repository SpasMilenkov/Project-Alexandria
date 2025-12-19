namespace API.Middlewares;

// TODO Test this with proper cookie support as currently swagger can't show me if there is a redirection problem or not
// TODO Potentially change response type to 401 if this is causing the 404
public sealed class JwtFromCookieMiddleware(RequestDelegate next)
{
    private const string BearerPrefix = "Bearer ";
    public async Task Invoke(HttpContext ctx)
    {
        if (!ctx.Request.Headers.ContainsKey("Authorization") &&
            ctx.Request.Cookies.TryGetValue("access_token", out var token))
        {
            ctx.Request.Headers.Authorization =  BearerPrefix + token;
        }
        await next(ctx);
    }
}