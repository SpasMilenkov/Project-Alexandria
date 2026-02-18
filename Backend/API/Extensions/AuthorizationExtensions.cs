using Common.Auth;
using Common.Seeding;

namespace API.Extensions;

public static class AuthorizationExtensions
{
    public static IServiceCollection AddAuthorizationPolicies(this IServiceCollection services)
    {
        services.AddAuthorizationBuilder()
            .AddDefaultPolicy(Policies.RequireUser, p => p.RequireRole(Roles.User, Roles.Admin))
            .AddPolicy(Policies.RequireAdmin, p => p.RequireRole(Roles.Admin))
            .AddPolicy(Policies.CanUpload, p => p.RequireRole(Roles.User, Roles.Admin))
            .AddPolicy(Policies.CanDelete, p => p.RequireRole(Roles.Admin))
            .AddPolicy(Policies.CanShare, p => p.RequireRole(Roles.User, Roles.Admin));

        return services;
    }
}
