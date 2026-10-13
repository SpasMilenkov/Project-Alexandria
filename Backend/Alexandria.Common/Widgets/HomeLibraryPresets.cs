using Alexandria.Data.Models.Enumerators.Widgets;

namespace Alexandria.Common.Widgets;

public static class HomeLibraryPresets
{
    public static IReadOnlyList<HomeLibraryDestination> Destinations(HomeShortcutGroup group) => group switch
    {
        HomeShortcutGroup.Files =>
            [HomeLibraryDestination.FileExplorer, HomeLibraryDestination.Tags,
                HomeLibraryDestination.AccessHistory, HomeLibraryDestination.Storage],
        HomeShortcutGroup.Media =>
            [HomeLibraryDestination.Music, HomeLibraryDestination.Videos,
                HomeLibraryDestination.Playlists, HomeLibraryDestination.YourListening],
        HomeShortcutGroup.Mixed =>
            [HomeLibraryDestination.FileExplorer, HomeLibraryDestination.Music,
                HomeLibraryDestination.Tags, HomeLibraryDestination.Playlists],
        _ => []
    };
}
