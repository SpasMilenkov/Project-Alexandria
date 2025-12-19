using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
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

// Standard .NET JWT Authentication
bld.Services.AddAuthentication(options =>
    {
        options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
        options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
    })
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = bld.Configuration["Jwt:Issuer"],
            ValidAudience = bld.Configuration["Jwt:Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(bld.Configuration["Jwt:Secret"]!))
        };
    });

bld.Services.AddAuthorization();

bld.WebHost.ConfigureKestrelMaxRequestSize();

var app = bld.Build();

app.UseCors("AllowOrigin");

app.UseMiddleware<JwtFromCookieMiddleware>();
app.UseAuthentication();
app.UseAuthorization();
// app.UseMiddleware<CsrfMiddleware>();

app.UseFastEndpoints(c =>
    {
        c.Versioning.Prefix = "v";
        c.Endpoints.RoutePrefix = "api";
        c.Versioning.PrependToRoute = true;
    }
    ).UseSwaggerGen();
app.MapHealthChecks("/health");

await app.SetupS3BucketAsync();
app.Run();