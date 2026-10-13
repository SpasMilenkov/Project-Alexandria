using Alexandria.Data.Context;
using Alexandria.Common.Auth;
using Alexandria.Data.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;

namespace Alexandria.Infrastructure;

public static class IdentityExtensions
{
    public static IServiceCollection AddAuthAndIdentity(this IServiceCollection services)
    {
        services.AddIdentity<ApplicationUser, ApplicationRole>(options =>
            {
                options.Password.RequireDigit = true;
                options.Password.RequireUppercase = true;
                options.Password.RequireNonAlphanumeric = true;
                options.Password.RequireLowercase = true;
                options.Password.RequiredLength = AccountValidationRules.PasswordLength;
                options.Password.RequiredUniqueChars = AccountValidationRules.PasswordUniqueCharacters;
                options.User.AllowedUserNameCharacters = AccountValidationRules.UsernameCharacters;
                options.SignIn.RequireConfirmedEmail = false;
            })
            .AddEntityFrameworkStores<AlexandriaDbContext>()
            .AddDefaultTokenProviders();

        return services;
    }
}
