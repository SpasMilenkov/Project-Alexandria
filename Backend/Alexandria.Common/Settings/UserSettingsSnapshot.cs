using Alexandria.Common.Settings.Values;

namespace Alexandria.Common.Settings;

public record UserSettingsSnapshot(
    AppearanceSettingsValue Appearance,
    BehaviorSettingsValue Behavior
);