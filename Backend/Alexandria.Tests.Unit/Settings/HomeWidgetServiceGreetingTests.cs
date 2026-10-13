using Alexandria.Common.Exceptions;
using Alexandria.Data.Models;
using Alexandria.Data.Models.Enumerators.Widgets;
using Alexandria.Dto.Home;
using AwesomeAssertions;
using NSubstitute;

namespace Alexandria.Tests.Unit.Settings;

public partial class HomeWidgetServiceTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public async Task SaveAsync_greeting_color_mode_round_trips_independently_of_mobile(int mode)
    {
        var desktop = Widget(HomeWidgetType.Greeting);
        var mobile = Widget(HomeWidgetType.Greeting, layout: HomeLayout.Mobile);
        const string mobileOptions = """{"greetingDecoration":8,"greetingColorMode":0}""";
        mobile.OptionsJson = mobileOptions;
        var (service, _) = CreateSut(desktop, mobile);

        await service.SaveAsync(Owner, new HomeBoardChanges
        {
            Updates = [new HomeWidgetUpdate(desktop.Id, Json($$$"""{"options":{"greetingColorMode":{{{mode}}}}}"""))]
        }, TestContext.Current.CancellationToken);

        await service.SaveAsync(Owner, new HomeBoardChanges
        {
            Updates = [new HomeWidgetUpdate(desktop.Id, Json("""{"options":{"greetingShowDate":true}}"""))]
        }, TestContext.Current.CancellationToken);

        var result = await service.GetAsync(Owner, TestContext.Current.CancellationToken);
        var options = result.Widgets.Single(row => row.Id == desktop.Id).Options;

        options.GetProperty("greetingColorMode").GetInt32().Should().Be(mode);
        options.GetProperty("greetingShowDate").GetBoolean().Should().BeTrue();
        mobile.OptionsJson.Should().Be(mobileOptions);
    }

    [Theory]
    [MemberData(nameof(HomeGreetingOptionsTests.Decorations), MemberType = typeof(HomeGreetingOptionsTests))]
    public async Task SaveAsync_each_greeting_decoration_can_be_created_and_changed_without_overwriting_mobile(HomeGreetingDecoration decoration)
    {
        var mobile = Widget(HomeWidgetType.Greeting, layout: HomeLayout.Mobile);
        const string mobileOptions = """{"greetingDecoration":1}""";
        mobile.OptionsJson = mobileOptions;
        var (service, _) = CreateSut(mobile);
        var id = Guid.NewGuid();

        await service.SaveAsync(Owner, new HomeBoardChanges
        {
            Creates = [new HomeWidgetCreate
            {
                Id = id, Layout = HomeLayout.Desktop, Type = HomeWidgetType.Greeting,
                Width = 6, Height = 4,
                Options = Json($$"""{"greetingDecoration":{{(int)decoration}}}""")
            }]
        }, TestContext.Current.CancellationToken);

        var created = await service.GetAsync(Owner, TestContext.Current.CancellationToken);
        created.Widgets.Single(row => row.Id == id).Options.GetProperty("greetingDecoration")
            .GetInt32().Should().Be((int)decoration);

        await service.SaveAsync(Owner, new HomeBoardChanges
        {
            Updates = [new HomeWidgetUpdate(id, Json("""{"options":{"greetingDecoration":1}}"""))]
        }, TestContext.Current.CancellationToken);

        await service.SaveAsync(Owner, new HomeBoardChanges
        {
            Updates = [new HomeWidgetUpdate(id, Json($$$"""{"options":{"greetingDecoration":{{{(int)decoration}}}}}"""))]
        }, TestContext.Current.CancellationToken);

        var updated = await service.GetAsync(Owner, TestContext.Current.CancellationToken);
        updated.Widgets.Single(row => row.Id == id).Options.GetProperty("greetingDecoration")
            .GetInt32().Should().Be((int)decoration);
        mobile.OptionsJson.Should().Be(mobileOptions);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public async Task SaveAsync_greeting_create_and_partial_update_preserve_mobile_and_header_preferences(int style)
    {
        var mobile = Widget(HomeWidgetType.Greeting, layout: HomeLayout.Mobile);
        const string mobileOptions = """{"greetingStyle":0,"greetingHeading":"Mobile","greetingDecoration":2}""";
        mobile.OptionsJson = mobileOptions;
        var (service, _) = CreateSut(mobile);
        var id = Guid.NewGuid();

        var original = await service.GetAsync(Owner, TestContext.Current.CancellationToken);
        original.Preferences.ShowHeaderGreeting.Should().BeTrue();

        await service.SaveAsync(Owner, new HomeBoardChanges
        {
            Creates = [new HomeWidgetCreate
            {
                Id = id, Layout = HomeLayout.Desktop, Type = HomeWidgetType.Greeting,
                Width = 6, Height = 4,
                Options = Json($$"""{"greetingStyle":{{style}},"greetingHeading":"  Welcome  ","greetingDecoration":3}""")
            }],
            Preferences = Json("""{"showHeaderGreeting":false}""")
        }, TestContext.Current.CancellationToken);

        await service.SaveAsync(Owner, new HomeBoardChanges
        {
            Updates = [new HomeWidgetUpdate(id, Json("""{"options":{"greetingShowDate":true}}"""))],
            Preferences = Json("""{"showWidgetTitles":true}""")
        }, TestContext.Current.CancellationToken);

        var result = await service.GetAsync(Owner, TestContext.Current.CancellationToken);
        var desktop = result.Widgets.Single(row => row.Id == id).Options;

        desktop.GetProperty("greetingStyle").GetInt32().Should().Be(style);
        desktop.GetProperty("greetingHeading").GetString().Should().Be("Welcome");
        desktop.GetProperty("greetingDecoration").GetInt32().Should().Be(3);
        desktop.GetProperty("greetingShowDate").GetBoolean().Should().BeTrue();
        mobile.OptionsJson.Should().Be(mobileOptions);
        result.Preferences.ShowHeaderGreeting.Should().BeFalse();
        result.Preferences.ShowWidgetTitles.Should().BeTrue();

        var restored = await service.SaveAsync(Owner, new HomeBoardChanges
        {
            Preferences = Json("""{"showHeaderGreeting":true}""")
        }, TestContext.Current.CancellationToken);

        restored.Preferences.ShowHeaderGreeting.Should().BeTrue();
    }

    [Theory]
    [InlineData(true, "{\"greetingStyle\":2}")]
    [InlineData(false, "{\"greetingDecoration\":19}")]
    [InlineData(false, "{\"greetingColorMode\":2}")]
    [InlineData(true, "{\"greetingColorMode\":-1}")]
    [InlineData(false, "{\"greetingColorMode\":\"1\"}")]
    [InlineData(true, "{\"greetingColorMode\":1,\"color\":\"red\"}")]
    [InlineData(true, "{\"greetingDecoration\":19}")]
    [InlineData(false, "{\"greetingDecoration\":\"4\"}")]
    [InlineData(true, "{\"greetingDecoration\":4.5}")]
    [InlineData(false, "{\"greetingDecoration\":4,\"svg\":\"<svg/>\"}")]
    [InlineData(true, "{\"greetingShowName\":null}")]
    [InlineData(false, "{\"greetingSubtitle\":null}")]
    [InlineData(true, "{\"greetingHeading\":\"a\\nb\"}")]
    [InlineData(false, "{\"greetingStyle\":0,\"greetingStyle\":1}")]
    [InlineData(true, "{\"greetingDecoration\":1,\"svg\":\"<svg/>\"}")]
    public async Task SaveAsync_invalid_greeting_create_or_update_does_not_mutate_any_tracked_state(bool create, string options)
    {
        var greeting = Widget(HomeWidgetType.Greeting);
        var other = Widget(HomeWidgetType.Storage, x: 4);
        var (service, unit) = CreateSut(greeting, other);
        var changes = new HomeBoardChanges
        {
            Updates = [new HomeWidgetUpdate(other.Id, Json("""{"x":6}"""))],
            Preferences = Json("""{"showHeaderGreeting":false}""")
        };

        if (create)
        {
            changes.Creates.Add(new HomeWidgetCreate
            {
                Id = Guid.NewGuid(), Type = HomeWidgetType.Greeting, Layout = HomeLayout.Desktop,
                X = 9, Width = 3, Height = 2, Options = Json(options)
            });
        }
        else
        {
            changes.Updates.Add(new HomeWidgetUpdate(greeting.Id, Json($$"""{"options":{{options}}}""")));
        }

        var action = () => service.SaveAsync(Owner, changes, TestContext.Current.CancellationToken);

        await action.Should().ThrowAsync<HomeWidgetValidationException>();
        other.X.Should().Be(4);
        greeting.OptionsJson.Should().Be("{}");
        unit.UserSettings.DidNotReceive().Update(Arg.Any<UserSettings>());
        await unit.HomeWidgets.DidNotReceive().AddRangeAsync(Arg.Any<IEnumerable<HomeWidget>>(), Arg.Any<CancellationToken>());
        await unit.DidNotReceive().CommitAsync(Arg.Any<CancellationToken>());

        var reloaded = await service.GetAsync(Owner, TestContext.Current.CancellationToken);
        reloaded.Preferences.ShowHeaderGreeting.Should().BeTrue();
        reloaded.Widgets.Should().HaveCount(2);
    }

    [Theory]
    [InlineData("{\"showHeaderGreeting\":null}")]
    [InlineData("{\"showHeaderGreeting\":1}")]
    [InlineData("{\"showHeaderGreeting\":\"false\"}")]
    [InlineData("{\"showHeaderGreeting\":false,\"showHeaderGreeting\":true}")]
    [InlineData("{\"ShowHeaderGreeting\":false}")]
    public async Task SaveAsync_invalid_header_preference_rejects_other_widget_edits(string preferences)
    {
        var widget = Widget(HomeWidgetType.Greeting);
        var (service, unit) = CreateSut(widget);
        var action = () => service.SaveAsync(Owner, new HomeBoardChanges
        {
            Updates = [new HomeWidgetUpdate(widget.Id, Json("""{"x":6}"""))],
            Preferences = Json(preferences)
        }, TestContext.Current.CancellationToken);

        await action.Should().ThrowAsync<HomeWidgetValidationException>();
        widget.X.Should().Be(0);
        unit.UserSettings.DidNotReceive().Update(Arg.Any<UserSettings>());
        await unit.DidNotReceive().CommitAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ResetAsync_hidden_header_returns_to_visible_default()
    {
        var (service, _) = CreateSut();
        await service.SaveAsync(Owner, new HomeBoardChanges
        {
            Preferences = Json("""{"showHeaderGreeting":false}""")
        }, TestContext.Current.CancellationToken);

        var result = await service.ResetAsync(Owner, TestContext.Current.CancellationToken);

        result.Preferences.ShowHeaderGreeting.Should().BeTrue();
    }
}
