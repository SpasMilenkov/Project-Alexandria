using API.Extensions;
using API.Features.Auth.Extensions;
using API.Middlewares;
using Infrastructure;

var bld = WebApplication.CreateBuilder();

bld.Services
    .AddHttpContextAccessor()
    .AddDatabase(bld.Configuration)
    .AddAuthAndIdentity()
    .AddJwtAuthentication(bld.Configuration)
    .AddAuthorizationPolicies()
    .AddS3Storage(bld.Configuration)
    .AddRabbitMqAsync(bld.Configuration)
    .AddApiServices()
    .AddServices()
    .AddHealthMonitoring(bld.Configuration)
    .AddAuthServices();

bld.WebHost.ConfigureKestrelMaxRequestSize();

var app = bld.Build();

await app.InitializeDatabaseAsync();

app.UseResponseCaching();
app.UseCors("AllowOrigin");
app.UseMiddleware<JwtFromCookieMiddleware>();
app.UseAuthentication();
app.UseAuthorization();
app.UseAlexandriaEndpoints();
app.MapHealthChecks("/health");

await app.RunAsync();
