using Alexandria.Api.Features.Auth;
using Alexandria.Common;
using Alexandria.Common.Config;
using Alexandria.Data.Models;
using Alexandria.Data.Models.Enumerators;
using AwesomeAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;
using Xunit;

namespace Alexandria.Tests.Unit.Auth;

public class GetSessionTests
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly AuthService _sut;

    public GetSessionTests()
    {
        _userManager = Substitute.For<UserManager<ApplicationUser>>(
            Substitute.For<IUserStore<ApplicationUser>>(),
            Substitute.For<IOptions<IdentityOptions>>(),
            Substitute.For<IPasswordHasher<ApplicationUser>>(),
            Array.Empty<IUserValidator<ApplicationUser>>(),
            Array.Empty<IPasswordValidator<ApplicationUser>>(),
            Substitute.For<ILookupNormalizer>(),
            Substitute.For<IdentityErrorDescriber>(),
            Substitute.For<IServiceProvider>(),
            Substitute.For<ILogger<UserManager<ApplicationUser>>>());

        _sut = new AuthService(
            _userManager,
            null!,
            Substitute.For<IUnitOfWork>(),
            null!);
    }

    [Fact]
    public async Task GetSessionAsync_activeUser_returnsRolesAndOnboardingStep()
    {
        var user = CreateUser(OnboardingStep.Tour);

        _userManager.FindByIdAsync(user.Id.ToString()).Returns(user);
        _userManager.GetRolesAsync(user).Returns(["Admin"]);

        var result = await _sut.GetSessionAsync(user.Id, TestContext.Current.CancellationToken);

        result.Succeeded.Should().BeTrue();
        result.User.Should().BeSameAs(user);
        result.UserRoles.Should().Equal("Admin");
        result.OnboardingStep.Should().Be(OnboardingStep.Tour);
    }

    [Fact]
    public async Task GetSessionAsync_missingUser_fails()
    {
        _userManager.FindByIdAsync(Arg.Any<string>()).Returns((ApplicationUser?)null);

        var result = await _sut.GetSessionAsync(Guid.NewGuid(), TestContext.Current.CancellationToken);

        result.Succeeded.Should().BeFalse();
    }

    [Fact]
    public async Task GetSessionAsync_deletedUser_fails()
    {
        var user = CreateUser(OnboardingStep.Done);
        user.DeletedAt = DateTime.UtcNow;

        _userManager.FindByIdAsync(user.Id.ToString()).Returns(user);

        var result = await _sut.GetSessionAsync(user.Id, TestContext.Current.CancellationToken);

        result.Succeeded.Should().BeFalse();
        await _userManager.DidNotReceiveWithAnyArgs().GetRolesAsync(default!);
    }

    [Fact]
    public async Task GetSessionAsync_systemUser_fails()
    {
        var user = CreateUser(OnboardingStep.Done);
        user.Id = SystemConfig.SystemId;

        _userManager.FindByIdAsync(user.Id.ToString()).Returns(user);

        var result = await _sut.GetSessionAsync(user.Id, TestContext.Current.CancellationToken);

        result.Succeeded.Should().BeFalse();
        await _userManager.DidNotReceiveWithAnyArgs().GetRolesAsync(default!);
    }

    private static ApplicationUser CreateUser(OnboardingStep onboardingStep)
    {
        return new ApplicationUser
        {
            Id = Guid.NewGuid(),
            Email = "user@example.com",
            Name = "User",
            UserName = "user",
            OnboardinStep = onboardingStep
        };
    }
}
