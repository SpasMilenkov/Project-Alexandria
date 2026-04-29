using Alexandria.Common.Services;
using Alexandria.Infrastructure;

namespace Alexandria.Api.Features.Auth.Extensions;

public static class AuthExtensions
{
    public static IServiceCollection AddAuthServices(this IServiceCollection services)
    {
        services.AddHostedService<RefreshTokenCleanupService>();
        services.AddScoped<IAuthService, AuthService>();

        return services;
    }
}