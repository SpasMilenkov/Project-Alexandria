using Alexandria.Common.Config;
using Alexandria.Common.Seeding;
using Alexandria.Data.Models;
using Alexandria.Data.Seeders;
using AwesomeAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace Alexandria.Tests.Unit.Infrastructure;

public class AdminSeederTests
{
    private const string AdminEmail = "owner@example.test";
    private const string AdminName = "owner";
    private const string AdminPassword = "OwnerPassword1!";
    private const string SensitiveDiagnostic = "sensitive-store-data";
    private readonly UserManager<ApplicationUser> _users;
    private readonly RoleManager<ApplicationRole> _roles;
    private readonly ILogger<AdminSeeder> _logger = Substitute.For<ILogger<AdminSeeder>>();
    private readonly IConfiguration _configuration;
    private ApplicationUser? _storedAdmin;
    private ApplicationUser? _storedSystem;
    private bool _adminRole;

    public AdminSeederTests()
    {
        _users = Substitute.For<UserManager<ApplicationUser>>(
            Substitute.For<IUserStore<ApplicationUser>>(),
            Substitute.For<IOptions<IdentityOptions>>(),
            Substitute.For<IPasswordHasher<ApplicationUser>>(),
            Array.Empty<IUserValidator<ApplicationUser>>(),
            Array.Empty<IPasswordValidator<ApplicationUser>>(),
            Substitute.For<ILookupNormalizer>(),
            new IdentityErrorDescriber(),
            Substitute.For<IServiceProvider>(),
            Substitute.For<ILogger<UserManager<ApplicationUser>>>());
        _roles = Substitute.For<RoleManager<ApplicationRole>>(
            Substitute.For<IRoleStore<ApplicationRole>>(),
            Array.Empty<IRoleValidator<ApplicationRole>>(),
            Substitute.For<ILookupNormalizer>(),
            new IdentityErrorDescriber(),
            Substitute.For<ILogger<RoleManager<ApplicationRole>>>());
        _configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Admin:Email"] = AdminEmail,
            ["Admin:Name"] = AdminName,
            ["Admin:Password"] = AdminPassword,
        }).Build();

        _roles.RoleExistsAsync(Arg.Any<string>()).Returns(true);
        _roles.CreateAsync(Arg.Any<ApplicationRole>()).Returns(IdentityResult.Success);
        _users.FindByEmailAsync(AdminEmail).Returns(_ => _storedAdmin);
        _users.FindByIdAsync(SystemConfig.SystemId.ToString()).Returns(_ => _storedSystem);
        _users.CreateAsync(Arg.Any<ApplicationUser>(), AdminPassword).Returns(call =>
        {
            _storedAdmin = call.ArgAt<ApplicationUser>(0);
            _storedAdmin.Id = Guid.NewGuid();
            return IdentityResult.Success;
        });
        _users.AddToRoleAsync(Arg.Any<ApplicationUser>(), Roles.Admin).Returns(_ =>
        {
            _adminRole = true;
            return IdentityResult.Success;
        });
        _users.IsInRoleAsync(Arg.Any<ApplicationUser>(), Roles.Admin).Returns(_ => _adminRole);
        _users.CheckPasswordAsync(Arg.Any<ApplicationUser>(), AdminPassword).Returns(true);
        _users.DeleteAsync(Arg.Any<ApplicationUser>()).Returns(_ =>
        {
            _storedAdmin = null;
            _adminRole = false;
            return IdentityResult.Success;
        });
        _users.CreateAsync(Arg.Any<ApplicationUser>()).Returns(call =>
        {
            _storedSystem = call.Arg<ApplicationUser>();
            return IdentityResult.Success;
        });
    }

    [Fact]
    public async Task SeedAsync_freshBootstrap_readsBackAndVerifiesAdminBeforeCreatingSystemAccount()
    {
        await CreateSut().SeedAsync(TestContext.Current.CancellationToken);

        _storedAdmin.Should().NotBeNull();
        _storedAdmin.Name.Should().Be(AdminName);
        _storedAdmin.UserName.Should().Be(AdminName);
        _storedAdmin.EmailConfirmed.Should().BeTrue();
        _storedAdmin.StorageQuota.Should().Be(ApplicationUser.DefaultStorageQuotaBytes);
        await _users.Received(2).FindByEmailAsync(AdminEmail);
        await _users.Received(1).CheckPasswordAsync(_storedAdmin, AdminPassword);
        await _users.Received(1).IsInRoleAsync(_storedAdmin, Roles.Admin);
        await _users.DidNotReceive().DeleteAsync(Arg.Any<ApplicationUser>());
        _storedSystem.Should().NotBeNull();
        _storedSystem.Id.Should().Be(SystemConfig.SystemId);
    }

    [Theory]
    [InlineData(Roles.Admin)]
    [InlineData(Roles.User)]
    public async Task SeedAsync_roleCreationFails_stopsBeforeAccountCreationWithSafeCodes(string role)
    {
        _roles.RoleExistsAsync(role).Returns(false);
        _roles.CreateAsync(Arg.Is<ApplicationRole>(value => value.Name == role))
            .Returns(FailedIdentity("DuplicateRoleName"));

        var act = () => CreateSut().SeedAsync(TestContext.Current.CancellationToken);

        var failure = await act.Should().ThrowAsync<InvalidOperationException>();
        AssertSafeFailure(failure.Which, "RoleCreationFailed", "DuplicateRoleName");
        await _users.DidNotReceive().FindByEmailAsync(Arg.Any<string>());
        await _users.DidNotReceive().CreateAsync(Arg.Any<ApplicationUser>(), Arg.Any<string>());
        await _users.DidNotReceive().CreateAsync(Arg.Any<ApplicationUser>());
    }

    [Fact]
    public async Task SeedAsync_completedBootstrapRerun_preservesCreatedRolesAndAccounts()
    {
        _roles.RoleExistsAsync(Roles.Admin).Returns(false, true);
        _roles.RoleExistsAsync(Roles.User).Returns(false, true);
        var sut = CreateSut();

        await sut.SeedAsync(TestContext.Current.CancellationToken);
        var admin = _storedAdmin;
        var system = _storedSystem;
        await sut.SeedAsync(TestContext.Current.CancellationToken);

        _storedAdmin.Should().BeSameAs(admin);
        _storedSystem.Should().BeSameAs(system);
        await _roles.Received(1).CreateAsync(Arg.Is<ApplicationRole>(role => role.Name == Roles.Admin));
        await _roles.Received(1).CreateAsync(Arg.Is<ApplicationRole>(role => role.Name == Roles.User));
        await _users.Received(1).CreateAsync(Arg.Any<ApplicationUser>(), AdminPassword);
        await _users.Received(1).CreateAsync(Arg.Any<ApplicationUser>());
        await _users.Received(1).CheckPasswordAsync(Arg.Any<ApplicationUser>(), AdminPassword);
    }

    [Fact]
    public async Task SeedAsync_adminCreationFails_stopsWithoutAttemptingRoleAssignmentOrDeletingAnyAccount()
    {
        _users.CreateAsync(Arg.Any<ApplicationUser>(), AdminPassword)
            .Returns(FailedIdentity("PasswordRequiresDigit"));

        var act = () => CreateSut().SeedAsync(TestContext.Current.CancellationToken);

        var failure = await act.Should().ThrowAsync<InvalidOperationException>();
        AssertSafeFailure(failure.Which, "AdminCreationFailed", "PasswordRequiresDigit");
        await _users.DidNotReceive().AddToRoleAsync(Arg.Any<ApplicationUser>(), Arg.Any<string>());
        await _users.DidNotReceive().DeleteAsync(Arg.Any<ApplicationUser>());
        await _users.DidNotReceive().CreateAsync(Arg.Any<ApplicationUser>());
    }

    [Fact]
    public async Task SeedAsync_unknownIdentityErrorCode_omitsUntrustedCodeAndDescription()
    {
        _users.CreateAsync(Arg.Any<ApplicationUser>(), AdminPassword)
            .Returns(FailedIdentity(SensitiveDiagnostic));

        var act = () => CreateSut().SeedAsync(TestContext.Current.CancellationToken);

        var failure = await act.Should().ThrowAsync<InvalidOperationException>();
        AssertSafeFailure(failure.Which, "AdminCreationFailed", "UnrecognizedIdentityError");
    }

    [Fact]
    public async Task SeedAsync_existingNonAdmin_doesNotPromoteResetOrDeleteAccount()
    {
        var existing = ExistingAccount();
        _storedAdmin = existing;

        var act = () => CreateSut().SeedAsync(TestContext.Current.CancellationToken);

        var failure = await act.Should().ThrowAsync<InvalidOperationException>();
        AssertSafeFailure(failure.Which, "ExistingAccountIsNotAdmin");
        _storedAdmin.Should().BeSameAs(existing);
        await _users.Received(1).IsInRoleAsync(existing, Roles.Admin);
        AssertNoExistingAccountMutation();
        await _users.DidNotReceive().CreateAsync(Arg.Any<ApplicationUser>());
    }

    [Fact]
    public async Task SeedAsync_existingAdmin_preservesPasswordWithoutRequiringFreshAccountConfiguration()
    {
        _storedAdmin = ExistingAccount();
        _adminRole = true;
        _configuration["Admin:Name"] = null;
        _configuration["Admin:Password"] = null;

        await CreateSut().SeedAsync(TestContext.Current.CancellationToken);

        await _users.Received(1).IsInRoleAsync(_storedAdmin, Roles.Admin);
        await _users.DidNotReceive().CheckPasswordAsync(Arg.Any<ApplicationUser>(), Arg.Any<string>());
        AssertNoExistingAccountMutation();
        _storedSystem.Should().NotBeNull();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SeedAsync_existingAccountIsDeletedOrReserved_failsWithoutMutation(bool reserved)
    {
        _storedAdmin = ExistingAccount();
        _adminRole = true;
        if (reserved)
            _storedAdmin.Id = SystemConfig.SystemId;
        else
            _storedAdmin.DeletedAt = new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc);

        var act = () => CreateSut().SeedAsync(TestContext.Current.CancellationToken);

        var failure = await act.Should().ThrowAsync<InvalidOperationException>();
        AssertSafeFailure(failure.Which, "ExistingAdminUnavailable");
        AssertNoExistingAccountMutation();
    }

    [Fact]
    public async Task SeedAsync_roleAssignmentFails_removesOnlyNewAccountAndAllowsRetry()
    {
        _users.AddToRoleAsync(Arg.Any<ApplicationUser>(), Roles.Admin)
            .Returns(FailedIdentity("UserNotInRole"), IdentityResult.Success);

        var act = () => CreateSut().SeedAsync(TestContext.Current.CancellationToken);

        var failure = await act.Should().ThrowAsync<InvalidOperationException>();
        AssertSafeFailure(failure.Which, "AdminRoleAssignmentFailed", "UserNotInRole");
        _storedAdmin.Should().BeNull();
        await _users.Received(1).DeleteAsync(Arg.Is<ApplicationUser>(user => user.Email == AdminEmail));
        await _users.DidNotReceive().CreateAsync(Arg.Any<ApplicationUser>());

        _adminRole = true;
        await CreateSut().SeedAsync(TestContext.Current.CancellationToken);

        _storedAdmin.Should().NotBeNull();
        _storedSystem.Should().NotBeNull();
    }

    [Fact]
    public async Task SeedAsync_rollbackFails_reportsRecoveryAndDoesNotPromotePartialAccountOnRetry()
    {
        _users.AddToRoleAsync(Arg.Any<ApplicationUser>(), Roles.Admin)
            .Returns(FailedIdentity("UserNotInRole"));
        _users.DeleteAsync(Arg.Any<ApplicationUser>()).Returns(FailedIdentity("ConcurrencyFailure"));

        var act = () => CreateSut().SeedAsync(TestContext.Current.CancellationToken);

        var failure = await act.Should().ThrowAsync<InvalidOperationException>();
        AssertSafeFailure(failure.Which, "AdminRollbackFailed", "ConcurrencyFailure");
        failure.Which.Message.Should().Contain("authorized recovery process");
        _storedAdmin.Should().NotBeNull();
        _adminRole = false;

        var retryFailure = await act.Should().ThrowAsync<InvalidOperationException>();

        retryFailure.Which.Message.Should().Contain("ExistingAccountIsNotAdmin");
        await _users.Received(1).CreateAsync(Arg.Any<ApplicationUser>(), AdminPassword);
        await _users.Received(1).AddToRoleAsync(Arg.Any<ApplicationUser>(), Roles.Admin);
        await _users.Received(1).DeleteAsync(Arg.Any<ApplicationUser>());
    }

    [Fact]
    public async Task SeedAsync_rollbackThrows_reportsSanitizedManualRecovery()
    {
        _users.AddToRoleAsync(Arg.Any<ApplicationUser>(), Roles.Admin)
            .Returns(FailedIdentity("UserNotInRole"));
        _users.DeleteAsync(Arg.Any<ApplicationUser>())
            .Returns<Task<IdentityResult>>(_ => throw new InvalidOperationException(SensitiveDiagnostic));

        var act = () => CreateSut().SeedAsync(TestContext.Current.CancellationToken);

        var failure = await act.Should().ThrowAsync<InvalidOperationException>();
        AssertSafeFailure(failure.Which, "AdminRollbackFailed");
        failure.Which.Message.Should().Contain("authorized recovery process");
        failure.Which.InnerException.Should().BeNull();
        _storedAdmin.Should().NotBeNull();
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("different-id")]
    [InlineData("password")]
    [InlineData("role")]
    public async Task SeedAsync_freshAdminCannotBeVerified_rollsBackAndFails(string failureKind)
    {
        if (failureKind == "missing")
            _users.FindByEmailAsync(AdminEmail).Returns((ApplicationUser?)null);
        else if (failureKind == "different-id")
            _users.FindByEmailAsync(AdminEmail).Returns((ApplicationUser?)null, ExistingAccount());
        else if (failureKind == "password")
            _users.CheckPasswordAsync(Arg.Any<ApplicationUser>(), AdminPassword).Returns(false);
        else
            _users.IsInRoleAsync(Arg.Any<ApplicationUser>(), Roles.Admin).Returns(false);

        var act = () => CreateSut().SeedAsync(TestContext.Current.CancellationToken);

        var failure = await act.Should().ThrowAsync<InvalidOperationException>();
        AssertSafeFailure(failure.Which, "AdminVerificationFailed");
        await _users.Received(1).DeleteAsync(Arg.Any<ApplicationUser>());
        await _users.DidNotReceive().CreateAsync(Arg.Any<ApplicationUser>());
    }

    [Fact]
    public async Task SeedAsync_identityStoreThrows_doesNotExposeItsExceptionOrInnerException()
    {
        _users.AddToRoleAsync(Arg.Any<ApplicationUser>(), Roles.Admin)
            .Returns<Task<IdentityResult>>(_ => throw new InvalidOperationException(
                $"{SensitiveDiagnostic} {AdminPassword} {AdminEmail}"));

        var act = () => CreateSut().SeedAsync(TestContext.Current.CancellationToken);

        var failure = await act.Should().ThrowAsync<InvalidOperationException>();
        AssertSafeFailure(failure.Which, "AdminRoleAssignmentFailed");
        failure.Which.InnerException.Should().BeNull();
        await _users.Received(1).DeleteAsync(Arg.Any<ApplicationUser>());
    }

    [Fact]
    public async Task SeedAsync_systemCreationFails_keepsVerifiedAdminForSafeRetry()
    {
        _users.CreateAsync(Arg.Any<ApplicationUser>())
            .Returns(FailedIdentity("DuplicateUserName"), IdentityResult.Success);

        var act = () => CreateSut().SeedAsync(TestContext.Current.CancellationToken);

        var failure = await act.Should().ThrowAsync<InvalidOperationException>();
        AssertSafeFailure(failure.Which, "SystemAccountCreationFailed", "DuplicateUserName");
        var admin = _storedAdmin;
        admin.Should().NotBeNull();
        await _users.DidNotReceive().DeleteAsync(Arg.Any<ApplicationUser>());

        await CreateSut().SeedAsync(TestContext.Current.CancellationToken);

        _storedAdmin.Should().BeSameAs(admin);
        await _users.Received(1).CreateAsync(Arg.Any<ApplicationUser>(), AdminPassword);
        await _users.Received(2).CreateAsync(Arg.Any<ApplicationUser>());
    }

    [Theory]
    [InlineData("Admin:Email")]
    [InlineData("Admin:Name")]
    [InlineData("Admin:Password")]
    public async Task SeedAsync_requiredFreshConfigurationMissing_failsBeforeCreatingAdmin(string key)
    {
        _configuration[key] = " ";

        var act = () => CreateSut().SeedAsync(TestContext.Current.CancellationToken);

        var failure = await act.Should().ThrowAsync<InvalidOperationException>();
        AssertSafeFailure(failure.Which, "AdminConfigurationMissing");
        failure.Which.Message.Should().Contain(key);
        await _users.DidNotReceive().CreateAsync(Arg.Any<ApplicationUser>(), Arg.Any<string>());
    }

    [Fact]
    public async Task SeedAsync_cancelledAfterAccountCreation_rollsBackBeforePropagatingCancellation()
    {
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        _users.CreateAsync(Arg.Any<ApplicationUser>(), AdminPassword).Returns(call =>
        {
            _storedAdmin = call.ArgAt<ApplicationUser>(0);
            cancellation.Cancel();
            return IdentityResult.Success;
        });

        var act = () => CreateSut().SeedAsync(cancellation.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
        _storedAdmin.Should().BeNull();
        await _users.Received(1).DeleteAsync(Arg.Any<ApplicationUser>());
        await _users.DidNotReceive().AddToRoleAsync(Arg.Any<ApplicationUser>(), Arg.Any<string>());
    }

    private AdminSeeder CreateSut() => new(_users, _roles, _logger, _configuration);

    private static ApplicationUser ExistingAccount() => new()
    {
        Id = Guid.NewGuid(),
        Name = "existing-owner",
        UserName = "existing-owner",
        Email = AdminEmail,
        PasswordHash = "existing-password-hash",
    };

    private static IdentityResult FailedIdentity(string code) => IdentityResult.Failed(new IdentityError
    {
        Code = code,
        Description = $"{SensitiveDiagnostic} {AdminEmail} {AdminPassword}",
    });

    private void AssertSafeFailure(InvalidOperationException failure, params string[] expectedCodes)
    {
        foreach (var code in expectedCodes)
            failure.Message.Should().Contain(code);

        var diagnostics = string.Join("\n", _logger.ReceivedCalls()
            .Where(call => call.GetMethodInfo().Name == nameof(ILogger.Log))
            .Select(call => call.GetArguments()[2]?.ToString()));
        diagnostics.Should().Contain(failure.Message);
        foreach (var sensitive in new[] { SensitiveDiagnostic, AdminEmail, AdminPassword })
        {
            failure.ToString().Should().NotContain(sensitive);
            diagnostics.Should().NotContain(sensitive);
        }
    }

    private void AssertNoExistingAccountMutation()
    {
        var mutationNames = new[]
        {
            nameof(UserManager<ApplicationUser>.CreateAsync),
            nameof(UserManager<ApplicationUser>.AddToRoleAsync),
            nameof(UserManager<ApplicationUser>.UpdateAsync),
            nameof(UserManager<ApplicationUser>.DeleteAsync),
            nameof(UserManager<ApplicationUser>.ResetPasswordAsync),
            nameof(UserManager<ApplicationUser>.ChangePasswordAsync),
        };
        _users.ReceivedCalls().Should().NotContain(call => mutationNames.Contains(call.GetMethodInfo().Name) &&
            !(call.GetMethodInfo().Name == nameof(UserManager<ApplicationUser>.CreateAsync) && call.GetArguments().Length == 1));
    }
}
