using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using Alexandria.Common;
using Alexandria.Common.Exceptions;
using Alexandria.Common.Services;
using Alexandria.Common.Settings.Keys;
using Alexandria.Common.Widgets;
using Alexandria.Data.Models;
using Alexandria.Data.Models.Enumerators.Widgets;
using Alexandria.Dto.Home;

namespace Alexandria.Services.User.Widgets;

public sealed class HomeWidgetService(IUnitOfWork unitOfWork) : IHomeWidgetService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<HomeBoardDto> GetAsync(Guid userId, CancellationToken ct = default)
    {
        var setting = await unitOfWork.UserSettings.GetByKeyAsync(UserSettingKeys.HomePreferences, userId, ct);
        var preferences = ReadPreferences(setting);
        var widgets = await unitOfWork.HomeWidgets.GetByUserAsync(userId, ct: ct);

        return new HomeBoardDto(preferences.Initialized, preferences.HasMobileLayout,
            new HomePreferencesDto(preferences.UseSeparateMobileLayout, preferences.ShowWidgetTitles,
                preferences.ShowHeaderGreeting),
            widgets.Select(widget => new HomeWidgetDto(widget.Id, widget.Layout, widget.Type,
                widget.X, widget.Y, widget.Width, widget.Height, widget.ShowTitle, widget.OptionsVersion,
                HomeWidgetOptionsCodec.NormalizeStored(widget.Type, widget.OptionsVersion,
                    Parse(widget.OptionsJson), $"Widgets[{widget.Id}]"))).ToList());
    }

    public async Task<HomeBoardDto> ResetAsync(Guid userId, CancellationToken ct = default)
    {
        var preferences = new HomePreferencesValue { Initialized = true };

        await unitOfWork.BeginTransactionAsync(ct);

        try
        {
            var widgets = await unitOfWork.HomeWidgets.GetByUserAsync(userId, ct: ct);
            var setting = await unitOfWork.UserSettings.GetByKeyAsync(UserSettingKeys.HomePreferences, userId, ct);

            unitOfWork.HomeWidgets.RemoveRange(widgets);
            await SavePreferencesAsync(userId, setting, preferences, setting?.Value ?? "", ct);
            await unitOfWork.CommitAsync(ct);
        }
        catch
        {
            await unitOfWork.RollbackAsync(CancellationToken.None);
            throw;
        }

        return new HomeBoardDto(true, false, new HomePreferencesDto(false, false), []);
    }

    public async Task<HomeBoardDto> SaveAsync(Guid userId, HomeBoardChanges changes, CancellationToken ct = default)
    {
        HomeWidgetBatchValidation.ValidateRequest(changes);

        await unitOfWork.BeginTransactionAsync(ct);

        try
        {
            var setting = await unitOfWork.UserSettings.GetByKeyAsync(UserSettingKeys.HomePreferences, userId, ct);
            var preferences = ReadPreferences(setting);
            var beforePreferences = JsonSerializer.Serialize(preferences, JsonOptions);
            var existing = await unitOfWork.HomeWidgets.GetByUserAsync(userId, ct: ct);
            var byId = existing.ToDictionary(widget => widget.Id);

            if (changes.Updates.Any(update => !byId.ContainsKey(update.Id)) ||
                changes.Deletes.Any(id => !byId.ContainsKey(id)))
                throw new HomeWidgetNotFoundException();

            var createIds = changes.Creates.Select(widget => widget.Id).ToArray();

            if (createIds.Length > 0 && await unitOfWork.HomeWidgets.AnyIdsExistAsync(createIds, ct))
                throw new HomeWidgetValidationException(
                    [new ValidationResult("A widget with this ID already exists.", ["Creates"])], 409);

            var proposed = existing.Select(Clone).ToDictionary(widget => widget.Id);

            for (var index = 0; index < changes.Updates.Count; index++)
            {
                var update = changes.Updates[index];
                HomeWidgetChanges.Apply(proposed[update.Id], update.Changes, $"Updates[{index}].Changes");
            }

            foreach (var id in changes.Deletes)
                proposed.Remove(id);

            var created = new List<HomeWidget>();

            for (var index = 0; index < changes.Creates.Count; index++)
            {
                var create = changes.Creates[index];
                var widget = new HomeWidget
                {
                    Id = create.Id, UserId = userId, UpdatedBy = userId, Layout = create.Layout, Type = create.Type,
                    X = create.X, Y = create.Y, Width = create.Width, Height = create.Height,
                    ShowTitle = create.ShowTitle, OptionsVersion = create.OptionsVersion,
                    OptionsJson = HomeWidgetOptionsCodec.Normalize(create.Type, create.OptionsVersion,
                        create.Options, $"Creates[{index}]").GetRawText()
                };

                proposed.Add(widget.Id, widget);
                created.Add(widget);
            }

            HomeWidgetChanges.Apply(preferences, changes.Preferences);
            preferences.Initialized = true;

            if (changes.InitializeMobileLayout || created.Any(widget => widget.Layout == HomeLayout.Mobile))
                preferences.HasMobileLayout = true;

            HomeWidgetBatchValidation.ValidateLayouts(proposed.Values.ToList(), preferences);

            foreach (var id in changes.Updates.Select(update => update.Id))
            {
                var stored = byId[id];
                var edited = proposed[id];

                if (stored.X == edited.X && stored.Y == edited.Y &&
                    stored.Width == edited.Width && stored.Height == edited.Height &&
                    stored.ShowTitle == edited.ShowTitle && stored.OptionsJson == edited.OptionsJson)
                    continue;

                stored.X = edited.X;
                stored.Y = edited.Y;
                stored.Width = edited.Width;
                stored.Height = edited.Height;
                stored.ShowTitle = edited.ShowTitle;
                stored.OptionsJson = edited.OptionsJson;
                stored.UpdatedAt = DateTime.UtcNow;
                stored.UpdatedBy = userId;
            }

            if (created.Count > 0)
                await unitOfWork.HomeWidgets.AddRangeAsync(created, ct);

            if (changes.Deletes.Count > 0)
                unitOfWork.HomeWidgets.RemoveRange(changes.Deletes.Select(id => byId[id]));

            await SavePreferencesAsync(userId, setting, preferences, beforePreferences, ct);
            await unitOfWork.CommitAsync(ct);
        }
        catch
        {
            await unitOfWork.RollbackAsync(CancellationToken.None);
            throw;
        }

        return await GetAsync(userId, ct);
    }

    private async Task SavePreferencesAsync(Guid userId, UserSettings? setting, HomePreferencesValue preferences,
        string before, CancellationToken ct)
    {
        var json = JsonSerializer.Serialize(preferences, JsonOptions);

        if (setting is null)
        {
            await unitOfWork.UserSettings.AddAsync(new UserSettings
            {
                UserId = userId, Key = UserSettingKeys.HomePreferences, Value = json, UpdatedBy = userId
            }, ct);
        }
        else if (before != json)
        {
            setting.Value = json;
            setting.UpdatedBy = userId;
            unitOfWork.UserSettings.Update(setting);
        }
    }

    private static HomePreferencesValue ReadPreferences(UserSettings? setting)
    {
        if (setting is null)
            return new HomePreferencesValue();

        return JsonSerializer.Deserialize<HomePreferencesValue>(setting.Value, JsonOptions)
            ?? throw new JsonException("Home preferences must be an object.");
    }

    private static JsonElement Parse(string json)
    {
        using var document = JsonDocument.Parse(json);

        return document.RootElement.Clone();
    }

    private static HomeWidget Clone(HomeWidget widget) => new()
    {
        Id = widget.Id, UserId = widget.UserId, Layout = widget.Layout, Type = widget.Type,
        X = widget.X, Y = widget.Y, Width = widget.Width, Height = widget.Height,
        ShowTitle = widget.ShowTitle, OptionsVersion = widget.OptionsVersion, OptionsJson = widget.OptionsJson
    };
}
