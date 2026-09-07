using Alexandria.Common;
using Alexandria.Common.Exceptions;
using Alexandria.Common.Services;
using Alexandria.Data.Models;
using Alexandria.Data.Models.Enumerators;
using Alexandria.Dto.Metrics;
using Alexandria.Dto.Users;
using Alexandria.Services.User;
using AwesomeAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace Alexandria.Tests.Unit.User;

public class UpdateUserQuotaTests
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly IStorageService _storage = Substitute.For<IStorageService>();
    private readonly UserManagementService _sut;
    private readonly Guid _userId = Guid.NewGuid();

    public UpdateUserQuotaTests()
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

        _sut = new UserManagementService(_userManager, _unitOfWork, _storage);
    }

    private ApplicationUser StoredUser(long quota = ApplicationUser.DefaultStorageQuotaBytes)
        => new()
        {
            Id = _userId,
            Name = "user",
            UserName = "user",
            Email = "user@example.com",
            CreatedAt = DateTime.UtcNow,
            StorageQuota = quota
        };

    private void StoredUserFound(ApplicationUser user)
    {
        _userManager.FindByIdAsync(_userId.ToString()).Returns(user);
        _userManager.UpdateAsync(user).Returns(IdentityResult.Success);
        _userManager.IsLockedOutAsync(user).Returns(false);
        _userManager.GetRolesAsync(user).Returns(new List<string>());
    }

    private static StorageBreakdown BreakdownWith(long usedBytes)
        => new()
        {
            SizeByType = new Dictionary<string, long>(),
            TrashSize = 0,
            OldFiles = [],
            FilesSize = usedBytes,
            UsedBytes = usedBytes
        };

    [Fact]
    public async Task create_user_defaults_quota_to_10gb()
    {
        var ct = TestContext.Current.CancellationToken;
        _userManager.FindByEmailAsync(Arg.Any<string>()).Returns((ApplicationUser?)null);
        _userManager.CreateAsync(Arg.Any<ApplicationUser>(), Arg.Any<string>())
            .Returns(IdentityResult.Success);
        _userManager.AddToRoleAsync(Arg.Any<ApplicationUser>(), Arg.Any<string>())
            .Returns(IdentityResult.Success);

        var result = await _sut.CreateUserAsync("user", "user@example.com", "Password1!", UserRole.User, null, ct);

        result.StorageQuota.Should().Be(ApplicationUser.DefaultStorageQuotaBytes);
    }

    [Fact]
    public async Task create_user_explicit_quota_stored()
    {
        var ct = TestContext.Current.CancellationToken;
        _userManager.FindByEmailAsync(Arg.Any<string>()).Returns((ApplicationUser?)null);
        _userManager.CreateAsync(Arg.Any<ApplicationUser>(), Arg.Any<string>())
            .Returns(IdentityResult.Success);
        _userManager.AddToRoleAsync(Arg.Any<ApplicationUser>(), Arg.Any<string>())
            .Returns(IdentityResult.Success);

        var result = await _sut.CreateUserAsync(
            "user", "user@example.com", "Password1!", UserRole.User, 5_000L, ct);

        result.StorageQuota.Should().Be(5_000L);
    }

    [Fact]
    public async Task create_user_negative_quota_throws_creation_exception()
    {
        var ct = TestContext.Current.CancellationToken;
        _userManager.FindByEmailAsync(Arg.Any<string>()).Returns((ApplicationUser?)null);

        var act = () => _sut.CreateUserAsync(
            "user", "user@example.com", "Password1!", UserRole.User, -1L, ct);

        var exception = await act.Should().ThrowAsync<UserCreationException>();
        exception.Which.Errors.Should().ContainKey("StorageQuotaBytes");
        await _userManager.DidNotReceive()
            .CreateAsync(Arg.Any<ApplicationUser>(), Arg.Any<string>());
    }

    [Fact]
    public async Task update_quota_below_usage_throws_quota_below_usage()
    {
        var ct = TestContext.Current.CancellationToken;
        StoredUserFound(StoredUser());
        _storage.GetStorageBreakdown(_userId, ct).Returns(BreakdownWith(1000L));

        var act = () => _sut.UpdateUserAsync(
            _userId, new UpdateUserDto { StorageQuotaBytes = 500L }, ct);

        var exception = await act.Should().ThrowAsync<StorageQuotaBelowUsageException>();
        exception.Which.QuotaBytes.Should().Be(500L);
        exception.Which.UsedBytes.Should().Be(1000L);
    }

    [Fact]
    public async Task update_quota_above_usage_stores()
    {
        var ct = TestContext.Current.CancellationToken;
        var user = StoredUser();
        StoredUserFound(user);
        _storage.GetStorageBreakdown(_userId, ct).Returns(BreakdownWith(1000L));

        var result = await _sut.UpdateUserAsync(
            _userId, new UpdateUserDto { StorageQuotaBytes = 2000L }, ct);

        result.StorageQuota.Should().Be(2000L);
        user.StorageQuota.Should().Be(2000L);
        await _userManager.Received(1).UpdateAsync(user);
    }

    [Fact]
    public async Task update_quota_zero_unlimited_skips_usage_check()
    {
        var ct = TestContext.Current.CancellationToken;
        var user = StoredUser();
        StoredUserFound(user);

        var result = await _sut.UpdateUserAsync(
            _userId, new UpdateUserDto { StorageQuotaBytes = 0L }, ct);

        result.StorageQuota.Should().Be(0L);
        await _storage.DidNotReceive().GetStorageBreakdown(
            Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task update_without_quota_keeps_existing()
    {
        var ct = TestContext.Current.CancellationToken;
        var user = StoredUser(quota: 777L);
        StoredUserFound(user);

        var result = await _sut.UpdateUserAsync(
            _userId, new UpdateUserDto { Email = "new@example.com" }, ct);

        result.StorageQuota.Should().Be(777L);
        await _storage.DidNotReceive().GetStorageBreakdown(
            Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task update_negative_quota_throws()
    {
        var ct = TestContext.Current.CancellationToken;
        StoredUserFound(StoredUser());

        var act = () => _sut.UpdateUserAsync(
            _userId, new UpdateUserDto { StorageQuotaBytes = -5L }, ct);

        await act.Should().ThrowAsync<InvalidOperationException>();
    }
}