using Alexandria.Data.Models;
using Alexandria.Data.Models.Enumerators;
using Alexandria.Tests.Common.Builders;
using Alexandria.Tests.Common.Fixtures;
using AwesomeAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Alexandria.Tests.Integration.Audit;

public class UsersAuditTriggerTests(DatabaseFixture fixture) : AuditTriggerTestBase(fixture)
{
    private async Task<ApplicationUser> SeedTargetAsync()
    {
        var target = new UserBuilder().WithUserName("target").Build();

        await AsUserAsync(db =>
        {
            db.Users.Add(target);
            return Task.CompletedTask;
        });

        return target;
    }

    private Task UpdateTargetAsync(Guid id, Action<ApplicationUser> change) =>
        AsUserAsync(async db => change(await db.Users.SingleAsync(u => u.Id == id, Ct)));

    [Fact]
    public async Task Insert_ownerIsTheNewUser()
    {
        var target = await SeedTargetAsync();

        var log = await SingleLogAsync(AuditEventCode.UserCreated);
        log.EntityId.Should().Be(target.Id);
        log.OwnerId.Should().Be(target.Id);
        log.UserId.Should().Be(UserId);
    }

    [Fact]
    public async Task ProfileChanges_writeOneRowEach()
    {
        var target = await SeedTargetAsync();

        await UpdateTargetAsync(target.Id, u => u.Email = "new@example.com");
        await UpdateTargetAsync(target.Id, u => u.StorageQuota = 1234);
        await UpdateTargetAsync(target.Id, u => u.Name = "Renamed");

        Metadata(await SingleLogAsync(AuditEventCode.UserEmailChanged)).GetProperty("new").GetString()
            .Should().Be("new@example.com");
        Metadata(await SingleLogAsync(AuditEventCode.UserQuotaChanged)).GetProperty("new").GetInt64()
            .Should().Be(1234);
        Metadata(await SingleLogAsync(AuditEventCode.UserRenamed)).GetProperty("field").GetString()
            .Should().Be("name");
    }

    [Fact]
    public async Task Lockout_isDecidedByLockoutEnd_notLockoutEnabled()
    {
        var target = await SeedTargetAsync();

        await UpdateTargetAsync(target.Id, u => u.LockoutEnabled = !u.LockoutEnabled);
        (await LogsAsync(AuditEventCode.UserLockedOut)).Should().BeEmpty();

        await UpdateTargetAsync(target.Id, u => u.LockoutEnd = DateTimeOffset.UtcNow.AddDays(1));
        await UpdateTargetAsync(target.Id, u => u.LockoutEnd = null);

        (await SingleLogAsync(AuditEventCode.UserLockedOut)).OwnerId.Should().Be(target.Id);
        (await SingleLogAsync(AuditEventCode.UserLockoutLifted)).OwnerId.Should().Be(target.Id);
    }

    [Fact]
    public async Task SoftDeleteAndRestore_writeBothEvents()
    {
        var target = await SeedTargetAsync();

        await UpdateTargetAsync(target.Id, u => u.DeletedAt = DateTime.UtcNow);
        await UpdateTargetAsync(target.Id, u => u.DeletedAt = null);

        (await SingleLogAsync(AuditEventCode.UserSoftDeleted)).EntityId.Should().Be(target.Id);
        (await SingleLogAsync(AuditEventCode.UserRestored)).EntityId.Should().Be(target.Id);
    }

    [Fact]
    public async Task RoleGrantAndRevoke_namesTheRole()
    {
        var target = await SeedTargetAsync();
        var role = new ApplicationRole { Id = Guid.NewGuid(), Name = "Admin", NormalizedName = "ADMIN" };

        await AsUserAsync(db =>
        {
            db.Roles.Add(role);
            return Task.CompletedTask;
        });
        await AsUserAsync(db =>
        {
            db.UserRoles.Add(new IdentityUserRole<Guid> { UserId = target.Id, RoleId = role.Id });
            return Task.CompletedTask;
        });
        await AsUserAsync(async db =>
            await db.UserRoles.Where(r => r.UserId == target.Id).ExecuteDeleteAsync(Ct));

        var granted = await SingleLogAsync(AuditEventCode.UserRoleGranted);
        granted.OwnerId.Should().Be(target.Id);
        Metadata(granted).GetProperty("role").GetString().Should().Be("Admin");
        (await SingleLogAsync(AuditEventCode.UserRoleRevoked)).EntityId.Should().Be(target.Id);
    }
}
