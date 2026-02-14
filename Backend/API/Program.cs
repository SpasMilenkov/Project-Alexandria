using System.Text;
using API.Features.Auth.Extensions;
using API.Middlewares;
using FastEndpoints;
using FastEndpoints.Swagger;
using Infrastructure;
using Infrastructure.Converters;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;

var bld = WebApplication.CreateBuilder();

bld.Services
    .AddHttpContextAccessor()
    .AddDatabase(bld.Configuration)
    .AddAuthAndIdentity()
    .AddS3Storage(bld.Configuration)
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

app.UseResponseCaching();
app.UseCors("AllowOrigin");

app.UseMiddleware<JwtFromCookieMiddleware>();
app.UseAuthentication();
app.UseAuthorization();

app.UseFastEndpoints(c =>
    {
        c.Versioning.Prefix = "v";
        c.Endpoints.RoutePrefix = "api";
        c.Versioning.PrependToRoute = true;
        c.Serializer.Options.Converters.Add(new BigIntegerJsonConverter());
    }
).UseSwaggerGen();
app.MapHealthChecks("/health");

await app.RunAsync();