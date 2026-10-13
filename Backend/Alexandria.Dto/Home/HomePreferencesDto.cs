namespace Alexandria.Dto.Home;

public sealed record HomePreferencesDto(
    bool UseSeparateMobileLayout, bool ShowWidgetTitles, bool ShowHeaderGreeting = true);
