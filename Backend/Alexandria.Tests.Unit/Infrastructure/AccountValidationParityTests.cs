using Alexandria.Common.Auth;
using Alexandria.Data.Models;
using Alexandria.Infrastructure;
using AwesomeAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace Alexandria.Tests.Unit.Infrastructure;

public class AccountValidationParityTests
{
    [Theory]
    [InlineData("Aa1!xy")]
    [InlineData("Good-Password1!")]
    [InlineData("short")]
    [InlineData("lowercase1!")]
    [InlineData("UPPERCASE1!")]
    [InlineData("NoNumber!")]
    [InlineData("NoSymbol1")]
    [InlineData("ÄÄÄäää١!")]
    public async Task ValidatePassword_installerAndActualIdentityValidator_agree(string password)
    {
        using var provider = CreateSut();
        var options = provider.GetRequiredService<IOptions<IdentityOptions>>();
        using var manager = new UserManager<ApplicationUser>(
            Substitute.For<IUserStore<ApplicationUser>>(), options,
            new PasswordHasher<ApplicationUser>(), [], [],
            new UpperInvariantLookupNormalizer(), new IdentityErrorDescriber(), provider,
            NullLogger<UserManager<ApplicationUser>>.Instance);

        var result = await new PasswordValidator<ApplicationUser>()
            .ValidateAsync(manager, new ApplicationUser { Name = "owner" }, password);

        result.Succeeded.Should().Be(AccountValidationRules.ValidatePassword(password) is null);
    }

    [Fact]
    public void AddAuthAndIdentity_installerContract_preservesTheEffectivePolicy()
    {
        using var provider = CreateSut();
        var options = provider.GetRequiredService<IOptions<IdentityOptions>>().Value;

        options.User.AllowedUserNameCharacters.Should().Be(AccountValidationRules.UsernameCharacters);
        options.Password.RequiredLength.Should().Be(6);
        options.Password.RequiredUniqueChars.Should().Be(1);
        options.Password.RequireDigit.Should().BeTrue();
        options.Password.RequireUppercase.Should().BeTrue();
        options.Password.RequireLowercase.Should().BeTrue();
        options.Password.RequireNonAlphanumeric.Should().BeTrue();
    }

    private static ServiceProvider CreateSut()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddAuthAndIdentity();

        return services.BuildServiceProvider();
    }
}
