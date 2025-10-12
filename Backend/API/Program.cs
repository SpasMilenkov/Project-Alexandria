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
    .AddServices();

bld.WebHost.ConfigureKestrelMaxRequestSize();

var app = bld.Build();

app.UseAuthentication();
app.UseAuthorization();
app.UseFastEndpoints().UseSwaggerGen();
app.MapHealthChecks("/health");
app.UseCors("AllowOrigin");

await app.SetupMinioBucketAsync();

app.Run();