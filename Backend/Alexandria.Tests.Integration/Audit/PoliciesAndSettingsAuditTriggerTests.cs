using System.Text.Json;
using Alexandria.Data.Models;
using Alexandria.Data.Models.Enumerators;
using Alexandria.Tests.Common.Fixtures;
using AwesomeAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Alexandria.Tests.Integration.Audit;

public class PoliciesAndSettingsAuditTriggerTests(DatabaseFixture fixture) : AuditTriggerTestBase(fixture)
{
    [Fact]
    public async Task PolicyAndRules_areFiledUnderTheirDirectory()
    {
        var directory = await SeedDirectoryAsync("Music");
        var policy = new DirectoryPolicy { Id = Guid.NewGuid(), DirectoryId = directory.Id, CreatedAt = DateTime.UtcNow };
        var rule = new PolicyRule
        {
            Id = Guid.NewGuid(),
            PolicyId = policy.Id,
            ActionType = PolicyActionType.AutoTag,
            TriggerType = PolicyTriggerType.AnyFile,
            TriggerValue = "*",
            Priority = 1,
            Parameters = JsonDocument.Parse("{\"apiKey\":\"never-logged\"}"),
            CreatedAt = DateTime.UtcNow,
        };

        await AsUserAsync(db =>
        {
            db.DirectoryPolicies.Add(policy);
            return Task.CompletedTask;
        });
        await AsUserAsync(db =>
        {
            db.PolicyRules.Add(rule);
            return Task.CompletedTask;
        });
        await AsUserAsync(async db => (await db.PolicyRules.SingleAsync(r => r.Id == rule.Id, Ct)).Priority = 5);
        await AsUserAsync(async db =>
            (await db.PolicyRules.SingleAsync(r => r.Id == rule.Id, Ct)).DeletedAt = DateTime.UtcNow);

        var created = await SingleLogAsync(AuditEventCode.PolicyCreated);
        created.ParentEntityId.Should().Be(directory.Id);
        created.EntityName.Should().Be("Music");

        (await SingleLogAsync(AuditEventCode.PolicyRuleCreated)).EntityId.Should().Be(policy.Id);

        var updated = Metadata(await SingleLogAsync(AuditEventCode.PolicyRuleUpdated));
        updated.GetProperty("changes").GetProperty("priority").GetProperty("new").GetInt32().Should().Be(5);

        (await SingleLogAsync(AuditEventCode.PolicyRuleDeleted)).OwnerId.Should().Be(UserId);

        (await AnyMetadataContainsAsync("never-logged")).Should().BeFalse();
    }

    [Fact]
    public async Task UserSetting_recordsChangedKeysOnly()
    {
        var setting = new UserSettings
        {
            Id = Guid.NewGuid(),
            UserId = UserId,
            Key = "user:appearance",
            Value = "{\"theme\":\"dark\",\"accent\":\"blue\"}",
            CreatedAt = DateTime.UtcNow,
        };

        await AsUserAsync(db =>
        {
            db.UserSettings.Add(setting);
            return Task.CompletedTask;
        });
        await AsUserAsync(async db =>
            (await db.UserSettings.SingleAsync(s => s.Id == setting.Id, Ct)).Value =
            "{\"theme\":\"light\",\"accent\":\"blue\"}");

        var rows = await LogsAsync(AuditEventCode.SettingChanged);
        rows.Should().HaveCount(2);
        rows[1].OwnerId.Should().Be(UserId);

        var changes = Metadata(rows[1]).GetProperty("changes");
        changes.GetProperty("theme").GetProperty("old").GetString().Should().Be("dark");
        changes.GetProperty("theme").GetProperty("new").GetString().Should().Be("light");
        changes.TryGetProperty("accent", out _).Should().BeFalse();
    }

    [Fact]
    public async Task AdminSetting_credentialLikeKeysAreRedacted()
    {
        var setting = new AdminSettings
        {
            Id = Guid.NewGuid(),
            Key = "admin:integration",
            Value = "{\"apiToken\":\"abc\",\"enabled\":true}",
            CreatedAt = DateTime.UtcNow,
        };

        await AsUserAsync(db =>
        {
            db.AdminSettings.Add(setting);
            return Task.CompletedTask;
        });

        var changes = Metadata(await SingleLogAsync(AuditEventCode.AdminSettingChanged)).GetProperty("changes");
        changes.GetProperty("apiToken").GetProperty("redacted").GetBoolean().Should().BeTrue();
        changes.GetProperty("enabled").GetProperty("new").GetBoolean().Should().BeTrue();
    }
}
