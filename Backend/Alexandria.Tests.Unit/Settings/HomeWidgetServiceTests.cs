using System.Text.Json;
using Alexandria.Common;
using Alexandria.Common.Exceptions;
using Alexandria.Common.Settings.Keys;
using Alexandria.Common.Widgets;
using Alexandria.Data.Models;
using Alexandria.Data.Models.Enumerators.Widgets;
using Alexandria.Dto.Home;
using Alexandria.Services.User.Widgets;
using AwesomeAssertions;
using NSubstitute;

namespace Alexandria.Tests.Unit.Settings;

public partial class HomeWidgetServiceTests
{
    private static readonly Guid Owner = Guid.NewGuid();

    private static JsonElement Json(string value)
    {
        using var document = JsonDocument.Parse(value);

        return document.RootElement.Clone();
    }

    private static HomeWidget Widget(HomeWidgetType type = HomeWidgetType.Clock, int x = 0,
        HomeLayout layout = HomeLayout.Desktop) => new()
    {
        Id = Guid.NewGuid(), UserId = Owner, Layout = layout, Type = type,
        X = x, Width = 2, Height = 2, OptionsJson = "{}"
    };

    private static (HomeWidgetService Service, IUnitOfWork Unit) CreateSut(params HomeWidget[] initial)
    {
        var unit = Substitute.For<IUnitOfWork>();
        var widgets = initial.ToList();
        UserSettings? setting = new()
        {
            UserId = Owner, Key = UserSettingKeys.HomePreferences,
            Value = """{"initialized":true,"hasMobileLayout":false,"useSeparateMobileLayout":false,"showWidgetTitles":false}"""
        };

        unit.UserSettings.GetByKeyAsync(UserSettingKeys.HomePreferences, Owner, TestContext.Current.CancellationToken)
            .Returns(_ => setting);
        unit.UserSettings.AddAsync(Arg.Any<UserSettings>(), TestContext.Current.CancellationToken)
            .Returns(call => setting = call.Arg<UserSettings>());
        unit.HomeWidgets.GetByUserAsync(Owner, ct: TestContext.Current.CancellationToken).Returns(_ => widgets);
        unit.HomeWidgets.When(repository => repository.AddRangeAsync(Arg.Any<IEnumerable<HomeWidget>>(),
            TestContext.Current.CancellationToken)).Do(call => widgets.AddRange(call.Arg<IEnumerable<HomeWidget>>()));
        unit.HomeWidgets.When(repository => repository.RemoveRange(Arg.Any<IEnumerable<HomeWidget>>()))
            .Do(call => widgets.RemoveAll(widget => call.Arg<IEnumerable<HomeWidget>>().Contains(widget)));

        return (new HomeWidgetService(unit), unit);
    }

    [Fact]
    public void Normalize_each_widget_uses_only_its_own_options_schema()
    {
        foreach (var type in Enum.GetValues<HomeWidgetType>())
        {
            var options = HomeWidgetOptionsCodec.Normalize(type, 1, Json("{}"), "Widget");

            options.ValueKind.Should().Be(JsonValueKind.Object);

            if (type == HomeWidgetType.Clock)
                options.TryGetProperty("timeZone", out _).Should().BeTrue();
            else
                options.TryGetProperty("timeZone", out _).Should().BeFalse();

            if (type == HomeWidgetType.Storage)
                options.TryGetProperty("storageStyle", out _).Should().BeTrue();
            else
                options.TryGetProperty("storageStyle", out _).Should().BeFalse();
        }
    }

    [Theory]
    [InlineData(HomeWidgetType.Clock, 2, "{}")]
    [InlineData(HomeWidgetType.Clock, 1, "{\"clockStyle\":6}")]
    [InlineData(HomeWidgetType.Clock, 1, "{\"timeZone\":\"Europe/MissingZone\"}")]
    [InlineData(HomeWidgetType.Storage, 1, "{\"clockStyle\":0}")]
    [InlineData(HomeWidgetType.Storage, 1, "{\"storageStyle\":99}")]
    [InlineData(HomeWidgetType.TopSongs, 1, "{\"limit\":1000}")]
    [InlineData(HomeWidgetType.Countdown, 1, "{\"countdownStyle\":99}")]
    [InlineData(HomeWidgetType.Countdown, 1, "{\"countdownLabel\":null}")]
    [InlineData(HomeWidgetType.Countdown, 1, "{\"countdownTarget\":\"2026-02-30\"}")]
    [InlineData(HomeWidgetType.FocusTimer, 1, "{\"focusMinutes\":0}")]
    [InlineData(HomeWidgetType.FocusTimer, 1, "{\"focusStyle\":3}")]
    [InlineData(HomeWidgetType.FocusTimer, 1, "{\"focusSessions\":9}")]
    [InlineData(HomeWidgetType.FocusTimer, 1, "{\"endsAt\":123456}")]
    [InlineData(HomeWidgetType.DailyCover, 1, "{\"dailyCoverStyle\":4}")]
    [InlineData(HomeWidgetType.DailyCover, 1, "{\"seed\":20261009}")]
    [InlineData(HomeWidgetType.LatestRecap, 1, "{\"recapPalette\":[\"#ffffff\"]}")]
    [InlineData(HomeWidgetType.LatestRecap, 1, "{\"recapPalette\":[\"red\",\"#ffffff\",\"#000000\"]}")]
    [InlineData(HomeWidgetType.LatestRecap, 1, "{\"recapPalette\":[null,\"#ffffff\",\"#000000\"]}")]
    [InlineData(HomeWidgetType.LatestRecap, 1, "{\"recapPalette\":[\"#ffffff\\n\",\"#ffffff\",\"#000000\"]}")]
    public void Normalize_invalid_or_foreign_options_are_rejected(HomeWidgetType type, int version, string json)
    {
        var action = () => HomeWidgetOptionsCodec.Normalize(type, version, Json(json), "Widget");

        action.Should().Throw<HomeWidgetValidationException>();
    }

    [Theory]
    [InlineData("null")]
    [InlineData("[\"#286789\",\"#4CCED4\",\"#ffe3b1\"]")]
    public void Normalize_recap_palette_preserves_colors_or_reset(string palette)
    {
        var options = HomeWidgetOptionsCodec.Normalize(HomeWidgetType.LatestRecap, 1,
            Json($"{{\"recapPalette\":{palette}}}"), "Widget");

        options.GetProperty("recapPalette").GetRawText().Should().Be(palette);
    }

    [Fact]
    public void Normalize_countdown_preserves_plain_text_and_enforces_label_length()
    {
        const string label = "<img src=x onerror=alert(1)> & party";
        var json = JsonSerializer.SerializeToElement(new { countdownLabel = label, countdownTarget = "2026-10-21" },
            new JsonSerializerOptions(JsonSerializerDefaults.Web));

        var options = HomeWidgetOptionsCodec.Normalize(HomeWidgetType.Countdown, 1, json, "Widget");

        options.GetProperty("countdownLabel").GetString().Should().Be(label);
        options.GetProperty("countdownTarget").GetString().Should().Be("2026-10-21");

        var oversized = JsonSerializer.SerializeToElement(new { countdownLabel = new string('a', 121) },
            new JsonSerializerOptions(JsonSerializerDefaults.Web));
        var action = () => HomeWidgetOptionsCodec.Normalize(HomeWidgetType.Countdown, 1, oversized, "Widget");

        action.Should().Throw<HomeWidgetValidationException>();
    }

    [Theory]
    [InlineData(HomeWidgetType.FocusTimer, "focusStyle", 3, "focusMinutes", "45")]
    [InlineData(HomeWidgetType.Clock, "clockStyle", 6, "timeZone", "\"Asia/Tokyo\"")]
    public async Task GetAsync_retired_style_falls_back_without_changing_other_options_or_widgets(
        HomeWidgetType type, string styleProperty, int retiredStyle, string otherProperty, string otherValue)
    {
        var widget = Widget(type, x: 4);
        widget.OptionsJson = $"{{\"{styleProperty}\":{retiredStyle},\"{otherProperty}\":{otherValue}}}";
        var untouched = Widget(HomeWidgetType.Storage);
        var (service, unit) = CreateSut(widget, untouched);

        var result = await service.GetAsync(Owner, TestContext.Current.CancellationToken);

        result.Widgets.Should().HaveCount(2).And.Contain(row => row.Id == untouched.Id);
        var recovered = result.Widgets.Single(row => row.Id == widget.Id);
        recovered.X.Should().Be(4);
        recovered.Options.GetProperty(styleProperty).GetInt32().Should().Be(0);
        recovered.Options.GetProperty(otherProperty).GetRawText().Should().Be(otherValue);
        widget.OptionsJson.Should().Contain($"\"{styleProperty}\":{retiredStyle}");
        await unit.DidNotReceive().CommitAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SaveAsync_unrelated_option_edit_can_recover_a_retired_style_but_cannot_write_it_again()
    {
        var focus = Widget(HomeWidgetType.FocusTimer);
        focus.OptionsJson = """{"focusStyle":3,"focusMinutes":45,"focusSessions":6}""";
        var (service, _) = CreateSut(focus);

        var saved = await service.SaveAsync(Owner, new HomeBoardChanges
        {
            Updates = [new HomeWidgetUpdate(focus.Id, Json("""{"options":{"shortBreakMinutes":10}}"""))]
        }, TestContext.Current.CancellationToken);

        saved.Widgets[0].Options.GetProperty("focusStyle").GetInt32().Should().Be(0);
        saved.Widgets[0].Options.GetProperty("focusMinutes").GetInt32().Should().Be(45);
        saved.Widgets[0].Options.GetProperty("focusSessions").GetInt32().Should().Be(6);
        saved.Widgets[0].Options.GetProperty("shortBreakMinutes").GetInt32().Should().Be(10);

        var invalidWrite = () => service.SaveAsync(Owner, new HomeBoardChanges
        {
            Updates = [new HomeWidgetUpdate(focus.Id, Json("""{"options":{"focusStyle":3}}"""))]
        }, TestContext.Current.CancellationToken);

        await invalidWrite.Should().ThrowAsync<HomeWidgetValidationException>();
    }

    [Theory]
    [InlineData(HomeWidgetType.FocusTimer, 1, "{\"focusStyle\":99}")]
    [InlineData(HomeWidgetType.Clock, 2, "{\"clockStyle\":6}")]
    [InlineData(HomeWidgetType.FocusTimer, 1, "{\"focusStyle\":3,\"focusMinutes\":0}")]
    public void NormalizeStored_unrecognized_invalid_state_still_requires_recovery(HomeWidgetType type,
        int version, string json)
    {
        var action = () => HomeWidgetOptionsCodec.NormalizeStored(type, version, Json(json), "Widget");

        action.Should().Throw<HomeWidgetValidationException>();
    }

    [Fact]
    public async Task ResetAsync_unreadable_widgets_and_preferences_are_replaced_with_an_initialized_empty_board()
    {
        var desktop = Widget(HomeWidgetType.FocusTimer);
        desktop.OptionsVersion = 999;
        desktop.OptionsJson = "not json";
        var mobile = Widget(layout: HomeLayout.Mobile);
        var (service, unit) = CreateSut(desktop, mobile);
        var setting = await unit.UserSettings.GetByKeyAsync(UserSettingKeys.HomePreferences, Owner,
            TestContext.Current.CancellationToken);
        setting!.Value = "not json";

        var result = await service.ResetAsync(Owner, TestContext.Current.CancellationToken);

        result.Initialized.Should().BeTrue();
        result.HasMobileLayout.Should().BeFalse();
        result.Preferences.Should().Be(new HomePreferencesDto(false, false));
        result.Widgets.Should().BeEmpty();
        var reloaded = await service.GetAsync(Owner, TestContext.Current.CancellationToken);
        reloaded.Initialized.Should().BeTrue();
        reloaded.Widgets.Should().BeEmpty();
        unit.UserSettings.Received(1).Update(setting);
        await unit.Received(1).BeginTransactionAsync(TestContext.Current.CancellationToken);
        await unit.Received(1).CommitAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task ResetAsync_without_preferences_creates_an_initialized_empty_board()
    {
        var (service, unit) = CreateSut();
        unit.UserSettings.GetByKeyAsync(UserSettingKeys.HomePreferences, Owner, TestContext.Current.CancellationToken)
            .Returns((UserSettings?)null);

        var result = await service.ResetAsync(Owner, TestContext.Current.CancellationToken);

        result.Initialized.Should().BeTrue();
        result.Widgets.Should().BeEmpty();
        await unit.UserSettings.Received(1).AddAsync(Arg.Is<UserSettings>(setting =>
            setting.UserId == Owner && setting.Key == UserSettingKeys.HomePreferences &&
            setting.Value.Contains("\"initialized\":true")), TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task ResetAsync_commit_failure_rolls_back_and_propagates_for_retry()
    {
        var (service, unit) = CreateSut(Widget());
        unit.CommitAsync(TestContext.Current.CancellationToken).Returns(Task.FromException(new IOException("offline")));

        var action = () => service.ResetAsync(Owner, TestContext.Current.CancellationToken);

        await action.Should().ThrowAsync<IOException>();
        await unit.Received(1).RollbackAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SaveAsync_stale_client_edit_preserves_pc_addition_position_and_independent_options()
    {
        var createdAt = new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc);
        var clock = Widget();
        clock.CreatedAt = createdAt;
        clock.X = 4;
        clock.ShowTitle = true;
        clock.OptionsJson = """{"timeZone":"UTC","showSeconds":true}""";
        var pcAddition = Widget(HomeWidgetType.Player);
        var (service, unit) = CreateSut(clock, pcAddition);
        var beforeSave = DateTime.UtcNow;

        var result = await service.SaveAsync(Owner, new HomeBoardChanges
        {
            Updates = [new HomeWidgetUpdate(clock.Id, Json("""{"options":{"timeZone":"Asia/Tokyo"}}"""))]
        }, TestContext.Current.CancellationToken);

        result.Widgets.Should().HaveCount(2);
        var saved = result.Widgets.Single(widget => widget.Id == clock.Id);
        saved.X.Should().Be(4);
        saved.ShowTitle.Should().BeTrue();
        saved.Options.GetProperty("showSeconds").GetBoolean().Should().BeTrue();
        saved.Options.GetProperty("timeZone").GetString().Should().Be("Asia/Tokyo");
        result.Widgets.Should().Contain(widget => widget.Id == pcAddition.Id);
        clock.CreatedAt.Should().Be(createdAt);
        clock.UpdatedAt.Should().BeOnOrAfter(beforeSave);
        clock.UpdatedBy.Should().Be(Owner);
        pcAddition.UpdatedAt.Should().BeNull();
        pcAddition.UpdatedBy.Should().BeNull();
        unit.HomeWidgets.DidNotReceive().Update(Arg.Any<HomeWidget>());
        unit.UserSettings.DidNotReceive().Update(Arg.Any<UserSettings>());
        await unit.Received(1).CommitAsync(TestContext.Current.CancellationToken);

        var updatedAt = clock.UpdatedAt;

        await service.SaveAsync(Owner, new HomeBoardChanges
        {
            Updates = [new HomeWidgetUpdate(clock.Id, Json("""{"x":4}"""))]
        }, TestContext.Current.CancellationToken);

        clock.UpdatedAt.Should().Be(updatedAt);
    }

    [Fact]
    public async Task SaveAsync_invalid_final_layout_rolls_back_without_mutating_tracked_rows()
    {
        var clock = Widget();
        var player = Widget(HomeWidgetType.Player, x: 4);
        var (service, unit) = CreateSut(clock, player);
        var changes = new HomeBoardChanges
        {
            Updates = [new HomeWidgetUpdate(clock.Id, Json("""{"x":4,"showTitle":true}"""))],
            Preferences = Json("""{"showWidgetTitles":true}""")
        };

        var action = () => service.SaveAsync(Owner, changes, TestContext.Current.CancellationToken);

        await action.Should().ThrowAsync<HomeWidgetValidationException>();
        clock.X.Should().Be(0);
        clock.ShowTitle.Should().BeNull();
        unit.UserSettings.DidNotReceive().Update(Arg.Any<UserSettings>());
        await unit.DidNotReceive().CommitAsync(Arg.Any<CancellationToken>());
        await unit.Received(1).RollbackAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SaveAsync_validates_after_explicit_deletions_and_clears_nullable_title()
    {
        var clock = Widget();
        clock.ShowTitle = false;
        var player = Widget(HomeWidgetType.Player, x: 4);
        var (service, _) = CreateSut(clock, player);

        var result = await service.SaveAsync(Owner, new HomeBoardChanges
        {
            Updates = [new HomeWidgetUpdate(clock.Id, Json("""{"x":4,"showTitle":null}"""))],
            Deletes = [player.Id]
        }, TestContext.Current.CancellationToken);

        result.Widgets.Should().ContainSingle().Which.Id.Should().Be(clock.Id);
        result.Widgets[0].X.Should().Be(4);
        result.Widgets[0].ShowTitle.Should().BeNull();
    }

    [Fact]
    public async Task SaveAsync_missing_or_foreign_widget_is_not_recreated()
    {
        var (service, unit) = CreateSut();
        var changes = new HomeBoardChanges
        {
            Updates = [new HomeWidgetUpdate(Guid.NewGuid(), Json("""{"x":2}"""))]
        };

        var action = () => service.SaveAsync(Owner, changes, TestContext.Current.CancellationToken);

        await action.Should().ThrowAsync<HomeWidgetNotFoundException>();
        await unit.HomeWidgets.DidNotReceive().AddRangeAsync(Arg.Any<IEnumerable<HomeWidget>>(), Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData("{\"type\":6}")]
    [InlineData("{\"userId\":\"foreign\"}")]
    [InlineData("{\"width\":\"4\"}")]
    [InlineData("{\"x\":2,\"x\":4}")]
    public async Task SaveAsync_invalid_change_fields_do_not_write(string json)
    {
        var clock = Widget();
        var (service, unit) = CreateSut(clock);
        var action = () => service.SaveAsync(Owner, new HomeBoardChanges
        {
            Updates = [new HomeWidgetUpdate(clock.Id, Json(json))]
        }, TestContext.Current.CancellationToken);

        await action.Should().ThrowAsync<HomeWidgetValidationException>();
        await unit.DidNotReceive().CommitAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SaveAsync_disabled_empty_mobile_layout_remains_initialized()
    {
        var (service, _) = CreateSut();

        var result = await service.SaveAsync(Owner, new HomeBoardChanges
        {
            InitializeMobileLayout = true
        }, TestContext.Current.CancellationToken);

        result.Initialized.Should().BeTrue();
        result.HasMobileLayout.Should().BeTrue();
        result.Preferences.UseSeparateMobileLayout.Should().BeFalse();
        result.Widgets.Should().BeEmpty();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public async Task SaveAsync_library_create_and_partial_update_preserve_independent_mobile_options(int style)
    {
        var mobile = Widget(HomeWidgetType.LibraryShortcuts, layout: HomeLayout.Mobile);
        mobile.OptionsJson = """{"libraryStyle":1,"shortcutGroup":0}""";
        var (service, _) = CreateSut(mobile);
        var id = Guid.NewGuid();

        var created = await service.SaveAsync(Owner, new HomeBoardChanges
        {
            Creates = [new HomeWidgetCreate
            {
                Id = id, Layout = HomeLayout.Desktop, Type = HomeWidgetType.LibraryShortcuts,
                Width = 3, Height = 2,
                Options = Json($$"""{"libraryStyle":{{style}},"shortcutGroup":1}""")
            }]
        }, TestContext.Current.CancellationToken);

        created.Widgets.Single(row => row.Id == id).Options.GetProperty("libraryStyle")
            .GetInt32().Should().Be(style);

        await service.SaveAsync(Owner, new HomeBoardChanges
        {
            Updates = [new HomeWidgetUpdate(id, Json("""{"options":{"shortcutGroup":0}}"""))]
        }, TestContext.Current.CancellationToken);

        var reloaded = await service.GetAsync(Owner, TestContext.Current.CancellationToken);
        var desktop = reloaded.Widgets.Single(row => row.Id == id);

        desktop.Options.GetProperty("libraryStyle").GetInt32().Should().Be(style);
        desktop.Options.GetProperty("shortcutGroup").GetInt32().Should().Be(0);
        reloaded.Widgets.Single(row => row.Id == mobile.Id).Options.GetProperty("libraryStyle")
            .GetInt32().Should().Be(1);
        mobile.OptionsJson.Should().Be("""{"libraryStyle":1,"shortcutGroup":0}""");
    }

    [Theory]
    [InlineData(true, "{\"libraryStyle\":4}")]
    [InlineData(false, "{\"libraryStyle\":-1}")]
    [InlineData(true, "{\"libraryStyle\":null}")]
    [InlineData(false, "{\"libraryStyle\":\"Directory\"}")]
    [InlineData(true, "{\"libraryStyle\":1.5}")]
    [InlineData(false, "{\"libraryStyle\":0,\"libraryStyle\":1}")]
    [InlineData(true, "{\"libraryStyle\":1,\"to\":\"/arbitrary\"}")]
    [InlineData(false, "{\"libraryStyle\":1,\"clockStyle\":0}")]
    [InlineData(true, "{\"shortcutGroup\":3,\"shortcuts\":[{\"destination\":14}]}")]
    [InlineData(false, "{\"shortcutGroup\":3,\"shortcuts\":[{\"destination\":0,\"destination\":1}]}")]
    [InlineData(true, "{\"shortcutGroup\":3,\"shortcuts\":[{\"destination\":0},{\"destination\":0}]}")]
    [InlineData(false, "{\"shortcutGroup\":3,\"shortcuts\":[{\"destination\":0}],\"featuredDestination\":6}")]
    [InlineData(true, "{\"shortcuts\":null}")]
    [InlineData(false, "{\"shortcutGroup\":3,\"shortcuts\":[{\"destination\":0,\"label\":\"   \"}]}")]
    [InlineData(true, "{\"shortcutGroup\":3,\"shortcuts\":[{\"destination\":0,\"route\":\"/x\"}]}")]
    [InlineData(false, "{\"shortcutGroup\":3,\"shortcuts\":[]}")]
    public async Task SaveAsync_invalid_library_create_or_update_rolls_back_the_entire_batch(
        bool create, string options)
    {
        var library = Widget(HomeWidgetType.LibraryShortcuts);
        library.OptionsJson = """{"libraryStyle":2,"shortcutGroup":1}""";
        var untouched = Widget(HomeWidgetType.Storage, x: 4);
        var (service, unit) = CreateSut(library, untouched);
        var changes = new HomeBoardChanges
        {
            Updates = [new HomeWidgetUpdate(untouched.Id, Json("""{"x":6}"""))],
            Preferences = Json("""{"showWidgetTitles":true}""")
        };

        if (create)
        {
            changes.Creates.Add(new HomeWidgetCreate
            {
                Id = Guid.NewGuid(), Layout = HomeLayout.Desktop, Type = HomeWidgetType.LibraryShortcuts,
                X = 9, Width = 3, Height = 2, Options = Json(options)
            });
        }
        else
        {
            changes.Updates.Add(new HomeWidgetUpdate(library.Id, Json($$"""{"options":{{options}}}""")));
        }

        var action = () => service.SaveAsync(Owner, changes, TestContext.Current.CancellationToken);

        await action.Should().ThrowAsync<HomeWidgetValidationException>();
        untouched.X.Should().Be(4);
        library.OptionsJson.Should().Be("""{"libraryStyle":2,"shortcutGroup":1}""");
        unit.UserSettings.DidNotReceive().Update(Arg.Any<UserSettings>());
        await unit.HomeWidgets.DidNotReceive().AddRangeAsync(Arg.Any<IEnumerable<HomeWidget>>(),
            Arg.Any<CancellationToken>());
        await unit.DidNotReceive().CommitAsync(Arg.Any<CancellationToken>());
        await unit.Received(1).RollbackAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SaveAsync_custom_destinations_round_trip_without_mutating_mobile_options()
    {
        var mobile = Widget(HomeWidgetType.LibraryShortcuts, layout: HomeLayout.Mobile);
        const string mobileJson = """{"shortcutGroup":3,"shortcuts":[{"destination":13,"label":"Highlights"}],"featuredDestination":13}""";
        mobile.OptionsJson = mobileJson;
        var (service, unit) = CreateSut(mobile);
        var id = Guid.NewGuid();

        await service.SaveAsync(Owner, new HomeBoardChanges
        {
            Creates = [new HomeWidgetCreate
            {
                Id = id, Layout = HomeLayout.Desktop, Type = HomeWidgetType.LibraryShortcuts,
                Width = 3, Height = 2,
                Options = Json("""{"libraryStyle":2,"shortcutGroup":3,"shortcuts":[{"destination":0,"label":"  Files  "},{"destination":6}],"featuredDestination":6}""")
            }]
        }, TestContext.Current.CancellationToken);

        await unit.HomeWidgets.Received(1).AddRangeAsync(
            Arg.Is<IEnumerable<HomeWidget>>(widgets => widgets.All(widget => widget.UpdatedBy == Owner)),
            TestContext.Current.CancellationToken);

        await service.SaveAsync(Owner, new HomeBoardChanges
        {
            Updates = [new HomeWidgetUpdate(id, Json("""{"options":{"shortcuts":[{"destination":6,"label":"Listen"},{"destination":0,"label":"Files"}]}}"""))]
        }, TestContext.Current.CancellationToken);

        var result = await service.GetAsync(Owner, TestContext.Current.CancellationToken);
        var desktopOptions = result.Widgets.Single(row => row.Id == id).Options;

        desktopOptions.GetProperty("shortcuts")[0].GetProperty("destination").GetInt32().Should().Be(6);
        desktopOptions.GetProperty("shortcuts")[1].GetProperty("label").GetString().Should().Be("Files");
        desktopOptions.GetProperty("featuredDestination").GetInt32().Should().Be(6);
        desktopOptions.GetProperty("libraryStyle").GetInt32().Should().Be(2);
        mobile.OptionsJson.Should().Be(mobileJson);
    }

    [Fact]
    public async Task SaveAsync_removing_a_stored_feature_rejects_the_entire_partial_batch()
    {
        const string stored = """{"shortcutGroup":3,"shortcuts":[{"destination":0},{"destination":6}],"featuredDestination":6}""";
        var library = Widget(HomeWidgetType.LibraryShortcuts);
        library.OptionsJson = stored;
        var other = Widget(HomeWidgetType.Storage, x: 4);
        var (service, unit) = CreateSut(library, other);

        var action = () => service.SaveAsync(Owner, new HomeBoardChanges
        {
            Updates = [
                new HomeWidgetUpdate(other.Id, Json("""{"x":6}""")),
                new HomeWidgetUpdate(library.Id, Json("""{"options":{"shortcuts":[{"destination":0}]}}"""))
            ],
            Preferences = Json("""{"showWidgetTitles":true}""")
        }, TestContext.Current.CancellationToken);

        await action.Should().ThrowAsync<HomeWidgetValidationException>();
        other.X.Should().Be(4);
        library.OptionsJson.Should().Be(stored);
        unit.UserSettings.DidNotReceive().Update(Arg.Any<UserSettings>());
        await unit.DidNotReceive().CommitAsync(Arg.Any<CancellationToken>());
        await unit.Received(1).RollbackAsync(Arg.Any<CancellationToken>());
    }
}
