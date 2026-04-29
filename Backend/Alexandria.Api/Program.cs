using Alexandria.Api.Extensions;
using Alexandria.Api.Features.Auth.Extensions;
using Alexandria.Api.Middlewares;
using Alexandria.Infrastructure;

var bld = WebApplication.CreateBuilder();

bld.Services
    .AddHttpContextAccessor()
    .AddDatabase(bld.Configuration)
    .AddAuthAndIdentity()
    .AddJwtAuthentication(bld.Configuration)
    .AddAuthorizationPolicies()
    .AddS3Storage(bld.Configuration)
    .AddRabbitMqAsync(bld.Configuration)
    .AddApiServices(bld.Configuration)
    .AddServices()
    .AddHealthMonitoring(bld.Configuration)
    .AddAuthServices();

bld.WebHost.ConfigureKestrelMaxRequestSize();

var app = bld.Build();

if (!app.Configuration.GetValue<bool>("SkipDatabaseInit"))
{
    await app.InitializeDatabaseAsync();
}

app.UseForwardedHeaders();
app.UseResponseCaching();
app.UseCors("AllowOrigin");
app.UseMiddleware<JwtFromCookieMiddleware>();
app.UseAuthentication();
app.UseAuthorization();
app.UseAlexandriaEndpoints();


app.MapHealthChecks("/health");

await app.RunAsync();

public partial class Program
{
}