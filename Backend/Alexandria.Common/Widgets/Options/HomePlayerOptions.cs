using System.ComponentModel.DataAnnotations;
using Alexandria.Data.Models.Enumerators.Widgets;

namespace Alexandria.Common.Widgets.Options;

public sealed class HomePlayerOptions
{
    [EnumDataType(typeof(HomePlayerStyle))]
    public HomePlayerStyle PlayerStyle { get; set; } = HomePlayerStyle.Vinyl;
}
