using Common;
using Common.Services;
using Infrastructure;

namespace API.Features.Auth.Extensions;

public static class AuthExtensions
{
    public static IServiceCollection AddAuthServices(this IServiceCollection services)
    {
        services.AddSingleton<CsrfService>();
        services.AddHostedService<RefreshTokenCleanupService>();
        services.AddScoped<IAuthService, AuthService>();
        
        return services;
    }
}