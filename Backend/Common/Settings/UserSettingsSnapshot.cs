using Common.Settings.Values;

namespace Common.Settings;

public record UserSettingsSnapshot(
    AppearanceSettingsValue Appearance,
    BehaviorSettingsValue Behavior
);
