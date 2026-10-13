using System.ComponentModel.DataAnnotations;
using Alexandria.Data.Models.Enumerators.Widgets;

namespace Alexandria.Common.Widgets.Options;

public sealed class HomeQuickAccessOptions
{
    [EnumDataType(typeof(HomeQuickAccessStyle))]
    public HomeQuickAccessStyle QuickAccessStyle { get; set; } = HomeQuickAccessStyle.Tiles;
}
