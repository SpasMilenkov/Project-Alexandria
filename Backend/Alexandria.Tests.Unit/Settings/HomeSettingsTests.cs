using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using Alexandria.Common;
using Alexandria.Common.Settings.Keys;
using Alexandria.Common.Settings.Values;
using Alexandria.Data.Models;
using Alexandria.Data.Models.Enumerators;
using Alexandria.Services.User.Settings;
using AwesomeAssertions;
using NSubstitute;

namespace Alexandria.Tests.Unit.Settings;

public class HomeSettingsTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static (UserSettingsService Service, IUnitOfWork Unit) CreateSut()
    {
        var unit = Substitute.For<IUnitOfWork>();

        return (new UserSettingsService(unit), unit);
    }

    [Fact]
    public async Task GetHomeAsync_missingSetting_returnsSharedDefaultWithoutWriting()
    {
        var (service, unit) = CreateSut();
        var owner = Guid.NewGuid();

        var result = await service.GetHomeAsync(owner, Ct);

        result.UseSeparateMobileLayout.Should().BeFalse();
        result.Mobile.Should().BeNull();
        result.Desktop.Widgets.Select(widget => widget.Type).Should()
            .Equal(HomeWidgetType.Clock, HomeWidgetType.LibraryShortcuts);
        await unit.UserSettings.Received(1).GetByKeyAsync(UserSettingKeys.Home, owner, Ct);
        await unit.DidNotReceive().SaveChangesAsync(Ct);
    }

    [Fact]
    public async Task GetHomeAsync_newerIncompatibleShape_preservesUnsupportedVersion()
    {
        var (service, unit) = CreateSut();
        var owner = Guid.NewGuid();
        var row = new UserSettings
        {
            UserId = owner, Key = UserSettingKeys.Home,
            Value = """{"schemaVersion":2,"desktop":{"widgets":[{"type":"future-widget"}]}}"""
        };
        unit.UserSettings.GetByKeyAsync(UserSettingKeys.Home, owner, Ct).Returns(row);

        var result = await service.GetHomeAsync(owner, Ct);

        result.SchemaVersion.Should().Be(2);
        await unit.DidNotReceive().SaveChangesAsync(Ct);
    }

    [Fact]
    public async Task SetHomeAsync_newSetting_roundTripsBothLayoutsForOwner()
    {
        var (service, unit) = CreateSut();
        var owner = Guid.NewGuid();
        var value = new HomeSettingsValue
        {
            UseSeparateMobileLayout = true,
            Mobile = new HomeLayoutValue { Columns = 4, Widgets = [] }
        };
        value.Desktop.Widgets[0].Options.TimeZone = "Asia/Tokyo";

        await service.SetHomeAsync(owner, value, owner, Ct);

        var added = (UserSettings)unit.UserSettings.ReceivedCalls()
            .Single(call => call.GetMethodInfo().Name == "AddAsync").GetArguments()[0]!;
        added.UserId.Should().Be(owner);
        added.UpdatedBy.Should().Be(owner);
        added.Key.Should().Be(UserSettingKeys.Home);
        unit.UserSettings.GetByKeyAsync(UserSettingKeys.Home, owner, Ct).Returns(added);

        var result = await service.GetHomeAsync(owner, Ct);

        result.Should().BeEquivalentTo(value);
        await unit.Received(1).SaveChangesAsync(Ct);
    }

    [Fact]
    public async Task SetHomeAsync_existingSetting_preservesDisabledMobileAndUpdatesOnlyOwner()
    {
        var (service, unit) = CreateSut();
        var owner = Guid.NewGuid();
        var row = new UserSettings { UserId = owner, Key = UserSettingKeys.Home, Value = "{}" };
        unit.UserSettings.GetByKeyAsync(UserSettingKeys.Home, owner, Ct).Returns(row);
        var value = new HomeSettingsValue { Mobile = HomeSettingsValue.CreateDefaultLayout(4) };
        value.Mobile.Widgets[0].Options.TimeZone = "UTC";
        value.Desktop.Widgets.Clear();

        await service.SetHomeAsync(owner, value, owner, Ct);

        unit.UserSettings.Received(1).Update(row);
        var stored = TypedSettingAccessor.GetValue<HomeSettingsValue>(row.Value);
        stored.Desktop.Widgets.Should().BeEmpty();
        stored.Mobile!.Widgets[0].Options.TimeZone.Should().Be("UTC");
        stored.UseSeparateMobileLayout.Should().BeFalse();
        await unit.Received(1).SaveChangesAsync(Ct);
    }

    [Fact]
    public async Task SetHomeAsync_invalidNestedOptions_rejectsBeforePersistence()
    {
        var (service, unit) = CreateSut();
        var value = new HomeSettingsValue();
        value.Desktop.Widgets[0].Options.TimeZone = "unsupported";

        var action = () => service.SetHomeAsync(Guid.NewGuid(), value, Guid.NewGuid(), Ct);

        await action.Should().ThrowAsync<ValidationException>();
        unit.UserSettings.ReceivedCalls().Should().BeEmpty();
        await unit.DidNotReceive().SaveChangesAsync(Ct);
    }

    [Fact]
    public void Validate_duplicateIdsAndMissingMobile_reportsMemberPaths()
    {
        var value = new HomeSettingsValue { UseSeparateMobileLayout = true };
        value.Desktop.Widgets[1].InstanceId = value.Desktop.Widgets[0].InstanceId;

        var paths = HomeSettingsValidation.Validate(value).SelectMany(error => error.MemberNames);

        paths.Should().Contain("Desktop.Widgets[1]").And.Contain("Mobile");
    }

    [Fact]
    public void Validate_repeatedTypesWithDistinctIds_acceptsLayout()
    {
        var value = new HomeSettingsValue();
        value.Desktop.Widgets[1].Type = HomeWidgetType.Clock;

        HomeSettingsValidation.Validate(value).Should().BeEmpty();
    }

    [Theory]
    [InlineData(33, 1)]
    [InlineData(2, 2)]
    public void Validate_excessWidgetsOrUnsupportedVersion_rejectsDocument(int count, int version)
    {
        var value = new HomeSettingsValue { SchemaVersion = version };
        value.Desktop.Widgets = Enumerable.Range(0, count)
            .Select(i => new HomeWidgetValue { InstanceId = $"clock-{i}" }).ToList();

        HomeSettingsValidation.Validate(value).Should().NotBeEmpty();
    }

    [Fact]
    public void NormalizeStored_invalidWidgetsAndDuplicateIds_keepsValidOrder()
    {
        var value = new HomeSettingsValue();
        value.Desktop.Widgets.Add(new HomeWidgetValue { InstanceId = "unknown", Type = (HomeWidgetType)99 });
        value.Desktop.Widgets.Add(new HomeWidgetValue { InstanceId = "bad-size", Size = (HomeWidgetSize)99 });
        value.Desktop.Widgets.Add(new HomeWidgetValue { InstanceId = "default-clock" });
        value.Desktop.Widgets.Add(null!);

        var result = HomeSettingsValidation.NormalizeStored(value);

        result.Desktop.Widgets.Select(widget => widget.InstanceId).Should()
            .Equal("default-clock", "default-library");
    }

    [Fact]
    public void NormalizeStored_emptyLayout_doesNotRepopulate()
    {
        var value = new HomeSettingsValue { Desktop = new HomeLayoutValue { Widgets = [] } };

        HomeSettingsValidation.NormalizeStored(value).Desktop.Widgets.Should().BeEmpty();
    }

    [Fact]
    public void NormalizeStored_missingLayout_recoversDefaultAndDisablesMissingMobile()
    {
        var value = new HomeSettingsValue { Desktop = null!, UseSeparateMobileLayout = true };

        var result = HomeSettingsValidation.NormalizeStored(value);

        result.Desktop.Widgets.Should().HaveCount(2);
        result.UseSeparateMobileLayout.Should().BeFalse();
    }

    [Fact]
    public void NormalizeStored_newerVersion_preservesVersionAndUnknownWidgets()
    {
        var value = new HomeSettingsValue { SchemaVersion = 2 };
        value.Desktop.Widgets[0].Type = (HomeWidgetType)99;

        var result = HomeSettingsValidation.NormalizeStored(value);

        result.SchemaVersion.Should().Be(2);
        result.Desktop.Widgets[0].Type.Should().Be((HomeWidgetType)99);
    }

    [Fact]
    public void Serialize_homeDocument_usesCamelCaseAndNumericEnums()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        var json = JsonSerializer.Serialize(new HomeSettingsValue(), options);
        using var document = JsonDocument.Parse(json);
        var widget = document.RootElement.GetProperty("desktop").GetProperty("widgets")[0];

        widget.GetProperty("type").GetInt32().Should().Be(0);
        widget.GetProperty("size").GetInt32().Should().Be(1);
        document.RootElement.GetProperty("schemaVersion").GetInt32().Should().Be(1);
        document.RootElement.GetProperty("desktop").GetProperty("columns").GetInt32().Should().Be(12);
        widget.GetProperty("width").GetInt32().Should().Be(6);
        widget.GetProperty("height").GetInt32().Should().Be(4);
        widget.GetProperty("options").GetProperty("shortcutGroup").GetInt32().Should().Be(0);
    }

    [Theory]
    [InlineData(-1, 0, 6, 4, "X")]
    [InlineData(7, 0, 6, 4, "X")]
    [InlineData(0, -1, 6, 4, "Y")]
    [InlineData(0, 253, 6, 4, "Y")]
    [InlineData(0, 0, 0, 4, "Width")]
    [InlineData(0, 0, 6, 13, "Height")]
    [InlineData(int.MaxValue, int.MaxValue, int.MaxValue, int.MaxValue, "X")]
    public void Validate_invalidGeometry_reportsMemberPath(int x, int y, int width, int height, string member)
    {
        var value = new HomeSettingsValue();
        var widget = value.Desktop.Widgets[0];
        widget.X = x;
        widget.Y = y;
        widget.Width = width;
        widget.Height = height;

        HomeSettingsValidation.Validate(value).SelectMany(error => error.MemberNames)
            .Should().Contain($"Desktop.Widgets[0].{member}");
    }

    [Fact]
    public void Validate_overlappingWidgets_rejectsButTouchingEdgesAreAllowed()
    {
        var value = new HomeSettingsValue();
        value.Desktop.Widgets[1].Y = 3;

        HomeSettingsValidation.Validate(value).SelectMany(error => error.MemberNames)
            .Should().Contain("Desktop.Widgets[1]");

        value.Desktop.Widgets[1].Y = 4;
        HomeSettingsValidation.Validate(value).Should().BeEmpty();
    }

    [Fact]
    public void Validate_wrongDeviceColumns_rejectsEvenEmptyLayouts()
    {
        var value = new HomeSettingsValue
        {
            Desktop = new HomeLayoutValue { Columns = 4 },
            Mobile = new HomeLayoutValue { Columns = 12 }
        };

        HomeSettingsValidation.Validate(value).SelectMany(error => error.MemberNames)
            .Should().Contain("Desktop.Columns").And.Contain("Mobile.Columns");
    }

    [Fact]
    public void NormalizeStored_invalidAndOverlappingGeometry_preservesValidGapsAndOptions()
    {
        var value = new HomeSettingsValue();
        value.Desktop.Widgets[0].X = 6;
        value.Desktop.Widgets[0].Options.TimeZone = "UTC";
        value.Desktop.Widgets[1].Y = 12;
        value.Desktop.Widgets.Add(new HomeWidgetValue { InstanceId = "overlap", X = 6 });
        value.Desktop.Widgets.Add(new HomeWidgetValue { InstanceId = "out-of-bounds", Y = 256 });

        var result = HomeSettingsValidation.NormalizeStored(value);

        result.Desktop.Widgets.Should().HaveCount(2);
        result.Desktop.Widgets[0].X.Should().Be(6);
        result.Desktop.Widgets[0].Options.TimeZone.Should().Be("UTC");
        result.Desktop.Widgets[1].Y.Should().Be(12);
        HomeSettingsValidation.Validate(result).Should().BeEmpty();
    }

    [Fact]
    public async Task SetHomeAsync_rightAlignedCoordinates_roundTripsWithoutCompaction()
    {
        var (service, unit) = CreateSut();
        var owner = Guid.NewGuid();
        var value = new HomeSettingsValue();
        value.Desktop.Widgets[0].X = 6;
        value.Desktop.Widgets[1].Y = 12;

        await service.SetHomeAsync(owner, value, owner, Ct);

        var added = (UserSettings)unit.UserSettings.ReceivedCalls()
            .Single(call => call.GetMethodInfo().Name == "AddAsync").GetArguments()[0]!;
        unit.UserSettings.GetByKeyAsync(UserSettingKeys.Home, owner, Ct).Returns(added);
        var result = await service.GetHomeAsync(owner, Ct);

        result.Should().BeEquivalentTo(value);
    }
}