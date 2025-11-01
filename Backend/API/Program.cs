using API.Features.Auth.Extensions;
using API.Middlewares;
using FastEndpoints;
using FastEndpoints.Swagger;
using Infrastructure;

var bld = WebApplication.CreateBuilder();

bld.Services
    .AddDatabase(bld.Configuration)
    .AddAuthAndIdentity()
    .AddMinio(bld.Configuration)
    .AddRabbitMqAsync(bld.Configuration)
    .AddApiServices()
    .AddServices()
    .AddAuthServices();

bld.WebHost.ConfigureKestrelMaxRequestSize();

var app = bld.Build();
app.UseCors("AllowOrigin");

// Middlewares
app.UseMiddleware<JwtFromCookieMiddleware>();
app.UseMiddleware<CsrfMiddleware>();
app.UseAuthentication();
app.UseAuthorization();
app.UseFastEndpoints().UseSwaggerGen();
app.MapHealthChecks("/health");

await app.SetupMinioBucketAsync();

app.Run();