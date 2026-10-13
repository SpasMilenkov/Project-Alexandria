using System.ComponentModel.DataAnnotations;
using Alexandria.Data.Models.Enumerators.Widgets;

namespace Alexandria.Common.Widgets.Options;

public sealed class HomeRecentFilesOptions
{
    [EnumDataType(typeof(HomeRecentFilesStyle))]
    public HomeRecentFilesStyle RecentFilesStyle { get; set; } = HomeRecentFilesStyle.List;

    [EnumDataType(typeof(HomeRecentFilesSource))]
    public HomeRecentFilesSource RecentFilesSource { get; set; } = HomeRecentFilesSource.Created;
}
