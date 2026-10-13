using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Alexandria.Common.Settings.Keys;
using Alexandria.Data.Context;
using Alexandria.Data.Models.Enumerators.Widgets;
using Alexandria.Dto.Home;
using Alexandria.Tests.Common.Fixtures;
using AwesomeAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Alexandria.Tests.Integration.FullStack.Home;

public class HomeWidgetApiTests(AlexandriaFixture fixture) : FullStackTestBase(fixture)
{
    private static HomeWidgetCreate Create(HomeWidgetType type = HomeWidgetType.Clock, int x = 0) => new()
    {
        Id = Guid.NewGuid(), Type = type, Layout = HomeLayout.Desktop, X = x, Width = 2, Height = 2,
        Options = Json("{}")
    };

    private static JsonElement Json(string value)
    {
        using var document = JsonDocument.Parse(value);

        return document.RootElement.Clone();
    }

    private async Task<HomeBoardDto> SaveAsync(HomeBoardChanges changes)
    {
        var response = await Auth.PatchAsJsonAsync("/api/home/widgets", changes, TestContext.Current.CancellationToken);
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        return (await response.Content.ReadFromJsonAsync<HomeBoardDto>(TestContext.Current.CancellationToken))!;
    }

    [Fact]
    public async Task Save_stale_phone_edit_preserves_pc_additions_and_independent_fields()
    {
        var clock = Create();
        await SaveAsync(new HomeBoardChanges { Creates = [clock] });

        var phoneBaseline = await Auth.GetFromJsonAsync<HomeBoardDto>("/api/home/widgets", TestContext.Current.CancellationToken);
        phoneBaseline!.Widgets.Should().ContainSingle();

        var player = Create(HomeWidgetType.Player, x: 6);
        await SaveAsync(new HomeBoardChanges
        {
            Creates = [player],
            Preferences = Json("""{"showHeaderGreeting":false}"""),
            Updates = [new HomeWidgetUpdate(clock.Id, Json("""{"x":2,"showTitle":true,"options":{"showSeconds":true}}"""))]
        });

        var result = await SaveAsync(new HomeBoardChanges
        {
            Updates = [new HomeWidgetUpdate(clock.Id, Json("""{"options":{"timeZone":"Asia/Tokyo"}}"""))]
        });

        result.Widgets.Should().HaveCount(2).And.Contain(widget => widget.Id == player.Id);
        result.Preferences.ShowHeaderGreeting.Should().BeFalse();
        var savedClock = result.Widgets.Single(widget => widget.Id == clock.Id);
        savedClock.X.Should().Be(2);
        savedClock.ShowTitle.Should().BeTrue();
        savedClock.Options.GetProperty("showSeconds").GetBoolean().Should().BeTrue();
        savedClock.Options.GetProperty("timeZone").GetString().Should().Be("Asia/Tokyo");

        using var scope = Factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AlexandriaDbContext>();
        var storedClock = await context.HomeWidgets.SingleAsync(widget => widget.Id == clock.Id,
            TestContext.Current.CancellationToken);

        using var options = JsonDocument.Parse(storedClock.OptionsJson);
        options.RootElement.TryGetProperty("storageStyle", out _).Should().BeFalse();

        var preferences = await context.UserSettings.SingleAsync(setting =>
            setting.UserId == UserId && setting.Key == UserSettingKeys.HomePreferences, TestContext.Current.CancellationToken);

        using var document = JsonDocument.Parse(preferences.Value);
        document.RootElement.GetProperty("showHeaderGreeting").GetBoolean().Should().BeFalse();
        document.RootElement.TryGetProperty("widgets", out _).Should().BeFalse();
    }

    [Fact]
    public async Task Save_invalid_or_foreign_changes_reject_the_whole_batch()
    {
        var clock = Create();
        var player = Create(HomeWidgetType.Player, x: 4);
        await SaveAsync(new HomeBoardChanges { Creates = [clock, player] });

        var invalid = await Auth.PatchAsJsonAsync("/api/home/widgets", new HomeBoardChanges
        {
            Updates = [new HomeWidgetUpdate(clock.Id, Json("""{"x":4}"""))],
            Preferences = Json("""{"showWidgetTitles":true}""")
        }, TestContext.Current.CancellationToken);
        invalid.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);

        var (other, _) = await CreateOtherUserAsync();
        using var otherClient = other;
        var foreign = await otherClient.PatchAsJsonAsync("/api/home/widgets", new HomeBoardChanges
        {
            Deletes = [clock.Id]
        }, TestContext.Current.CancellationToken);
        foreign.StatusCode.Should().Be(HttpStatusCode.NotFound);

        var collision = await otherClient.PatchAsJsonAsync("/api/home/widgets", new HomeBoardChanges
        {
            Creates = [clock]
        }, TestContext.Current.CancellationToken);
        collision.StatusCode.Should().Be(HttpStatusCode.Conflict);

        var anonymous = await Anon.PatchAsJsonAsync("/api/home/widgets", new HomeBoardChanges(),
            TestContext.Current.CancellationToken);
        anonymous.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        var saved = await Auth.GetFromJsonAsync<HomeBoardDto>("/api/home/widgets", TestContext.Current.CancellationToken);
        saved!.Widgets.Single(widget => widget.Id == clock.Id).X.Should().Be(0);
        saved.Preferences.ShowWidgetTitles.Should().BeFalse();
        saved.Widgets.Should().HaveCount(2);

        var foreignBoard = await otherClient.GetFromJsonAsync<HomeBoardDto>("/api/home/widgets", TestContext.Current.CancellationToken);
        foreignBoard!.Widgets.Should().BeEmpty();
        foreignBoard.Initialized.Should().BeFalse();
    }

    [Fact]
    public async Task Save_empty_layouts_and_mobile_preferences_survive_reload_and_title_inheritance()
    {
        var initial = await Auth.GetFromJsonAsync<HomeBoardDto>("/api/home/widgets", TestContext.Current.CancellationToken);
        initial!.Initialized.Should().BeFalse();

        var clock = Create();
        clock.ShowTitle = false;
        await SaveAsync(new HomeBoardChanges { Creates = [clock] });

        var inherited = await SaveAsync(new HomeBoardChanges
        {
            InitializeMobileLayout = true,
            Preferences = Json("""{"useSeparateMobileLayout":true,"showWidgetTitles":true}"""),
            Updates = [new HomeWidgetUpdate(clock.Id, Json("""{"showTitle":null}"""))]
        });
        inherited.Widgets.Should().ContainSingle().Which.ShowTitle.Should().BeNull();

        await SaveAsync(new HomeBoardChanges
        {
            Deletes = [clock.Id],
            Preferences = Json("""{"useSeparateMobileLayout":false}""")
        });

        var saved = await Auth.GetFromJsonAsync<HomeBoardDto>("/api/home/widgets", TestContext.Current.CancellationToken);
        saved!.Initialized.Should().BeTrue();
        saved.HasMobileLayout.Should().BeTrue();
        saved.Widgets.Should().BeEmpty();
        saved.Preferences.UseSeparateMobileLayout.Should().BeFalse();
        saved.Preferences.ShowWidgetTitles.Should().BeTrue();
    }

    [Fact]
    public async Task Get_retired_focus_style_recovers_and_current_writes_remain_strict()
    {
        var focus = Create(HomeWidgetType.FocusTimer);
        await SaveAsync(new HomeBoardChanges { Creates = [focus] });

        using (var scope = Factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<AlexandriaDbContext>();
            var stored = await context.HomeWidgets.SingleAsync(widget => widget.Id == focus.Id,
                TestContext.Current.CancellationToken);
            stored.OptionsJson = """{"focusStyle":3,"focusMinutes":45,"focusSessions":6}""";
            await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        var loaded = await Auth.GetFromJsonAsync<HomeBoardDto>("/api/home/widgets", TestContext.Current.CancellationToken);
        loaded!.Widgets[0].Options.GetProperty("focusStyle").GetInt32().Should().Be(0);
        loaded.Widgets[0].Options.GetProperty("focusMinutes").GetInt32().Should().Be(45);

        var invalid = await Auth.PatchAsJsonAsync("/api/home/widgets", new HomeBoardChanges
        {
            Updates = [new HomeWidgetUpdate(focus.Id, Json("""{"options":{"focusStyle":3}}"""))]
        }, TestContext.Current.CancellationToken);
        invalid.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);

        var saved = await SaveAsync(new HomeBoardChanges
        {
            Updates = [new HomeWidgetUpdate(focus.Id, Json("""{"options":{"shortBreakMinutes":10}}"""))]
        });
        saved.Widgets[0].Options.GetProperty("focusStyle").GetInt32().Should().Be(0);
        saved.Widgets[0].Options.GetProperty("focusSessions").GetInt32().Should().Be(6);
    }

    [Fact]
    public async Task Reset_unreadable_board_removes_both_layouts_only_for_the_authenticated_owner()
    {
        var desktop = Create();
        var mobile = Create();
        mobile.Layout = HomeLayout.Mobile;
        await SaveAsync(new HomeBoardChanges { Creates = [desktop, mobile], InitializeMobileLayout = true });
        var (other, otherId) = await CreateOtherUserAsync();
        using var otherClient = other;
        var foreign = Create();
        var otherSave = await otherClient.PatchAsJsonAsync("/api/home/widgets",
            new HomeBoardChanges { Creates = [foreign] }, TestContext.Current.CancellationToken);
        otherSave.StatusCode.Should().Be(HttpStatusCode.OK);

        using (var scope = Factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<AlexandriaDbContext>();
            var stored = await context.HomeWidgets.SingleAsync(widget => widget.Id == desktop.Id,
                TestContext.Current.CancellationToken);
            stored.OptionsVersion = 999;
            await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        var broken = await Auth.GetAsync("/api/home/widgets", TestContext.Current.CancellationToken);
        broken.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        var anonymous = await Anon.DeleteAsync("/api/home/widgets", TestContext.Current.CancellationToken);
        anonymous.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        using (var scope = Factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<AlexandriaDbContext>();
            var preferences = await context.UserSettings.SingleAsync(setting =>
                setting.UserId == UserId && setting.Key == UserSettingKeys.HomePreferences,
                TestContext.Current.CancellationToken);
            preferences.Value = "null";
            await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        var response = await Auth.DeleteAsync("/api/home/widgets", TestContext.Current.CancellationToken);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var cleared = await response.Content.ReadFromJsonAsync<HomeBoardDto>(TestContext.Current.CancellationToken);
        cleared!.Initialized.Should().BeTrue();
        cleared.HasMobileLayout.Should().BeFalse();
        cleared.Widgets.Should().BeEmpty();
        cleared.Preferences.Should().Be(new HomePreferencesDto(false, false));

        var reloaded = await Auth.GetFromJsonAsync<HomeBoardDto>("/api/home/widgets", TestContext.Current.CancellationToken);
        reloaded!.Initialized.Should().BeTrue();
        reloaded.Widgets.Should().BeEmpty();
        var otherBoard = await otherClient.GetFromJsonAsync<HomeBoardDto>("/api/home/widgets", TestContext.Current.CancellationToken);
        otherBoard!.Widgets.Should().ContainSingle().Which.Id.Should().Be(foreign.Id);

        using var verificationScope = Factory.Services.CreateScope();
        var verificationContext = verificationScope.ServiceProvider.GetRequiredService<AlexandriaDbContext>();
        (await verificationContext.HomeWidgets.CountAsync(widget => widget.UserId == UserId,
            TestContext.Current.CancellationToken)).Should().Be(0);
        (await verificationContext.HomeWidgets.CountAsync(widget => widget.UserId == otherId,
            TestContext.Current.CancellationToken)).Should().Be(1);
    }
}
