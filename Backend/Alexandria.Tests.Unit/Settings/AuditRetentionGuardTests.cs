using Alexandria.Common;
using Alexandria.Common.Repositories;
using Alexandria.Common.Settings.Keys;
using Alexandria.Common.Settings.Values;
using Alexandria.Data.Models;
using Alexandria.Services.User.Settings;
using AwesomeAssertions;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace Alexandria.Tests.Unit.Settings;

public class AuditRetentionGuardTests
{
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly IAdminSettingsRepository _settings = Substitute.For<IAdminSettingsRepository>();

    public AuditRetentionGuardTests()
    {
        _unitOfWork.AdminSettings.Returns(_settings);
    }

    private AdminSettingsService CreateSut() => new(_unitOfWork,
        new MemoryCache(new MemoryCacheOptions { SizeLimit = 100 }), NullLogger<AdminSettingsService>.Instance);

    private void Store(string? json)
    {
        var row = json is null ? null : new AdminSettings { Key = AdminSettingKeys.AuditRetention, Value = json };

        _settings.GetByKeyAsync(AdminSettingKeys.AuditRetention, Arg.Any<CancellationToken>()).Returns(row);
    }

    [Theory]
    [InlineData(0, AuditRetentionValue.MinDays)]
    [InlineData(-5, AuditRetentionValue.MinDays)]
    [InlineData(29, AuditRetentionValue.MinDays)]
    [InlineData(99_999, AuditRetentionValue.MaxDays)]
    public async Task GetAuditRetentionAsync_storedOutOfRange_isClampedIntoRange(int stored, int expected)
    {
        Store($$"""{"retentionDays":{{stored}}}""");

        var value = await CreateSut().GetAuditRetentionAsync(TestContext.Current.CancellationToken);

        value.RetentionDays.Should().Be(expected);
    }

    [Theory]
    [InlineData(AuditRetentionValue.MinDays)]
    [InlineData(365)]
    [InlineData(AuditRetentionValue.MaxDays)]
    public async Task GetAuditRetentionAsync_storedInRange_isReturnedUnchanged(int stored)
    {
        Store($$"""{"retentionDays":{{stored}}}""");

        var value = await CreateSut().GetAuditRetentionAsync(TestContext.Current.CancellationToken);

        value.RetentionDays.Should().Be(stored);
    }

    [Fact]
    public async Task GetAuditRetentionAsync_nothingStored_usesTheDefault()
    {
        Store(null);

        var value = await CreateSut().GetAuditRetentionAsync(TestContext.Current.CancellationToken);

        value.RetentionDays.Should().Be(AuditRetentionValue.DefaultDays);
    }
}
