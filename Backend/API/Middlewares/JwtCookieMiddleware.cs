namespace API.Middlewares;

public sealed class JwtFromCookieMiddleware(RequestDelegate next)
{
    private const string BearerPrefix = "Bearer ";
    public async Task Invoke(HttpContext ctx)
    {
        if (!ctx.Request.Headers.ContainsKey("Authorization") &&
            ctx.Request.Cookies.TryGetValue("access_token", out var token))
        {
            ctx.Request.Headers.Authorization = BearerPrefix + token;
        }
        await next(ctx);
    }
}