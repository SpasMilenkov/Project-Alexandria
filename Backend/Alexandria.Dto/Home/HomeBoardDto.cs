namespace Alexandria.Dto.Home;

public sealed record HomeBoardDto(
    bool Initialized, bool HasMobileLayout, HomePreferencesDto Preferences,
    IReadOnlyList<HomeWidgetDto> Widgets);
