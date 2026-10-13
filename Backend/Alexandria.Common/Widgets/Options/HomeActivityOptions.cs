using System.ComponentModel.DataAnnotations;
using Alexandria.Data.Models.Enumerators.Widgets;

namespace Alexandria.Common.Widgets.Options;

public sealed class HomeActivityOptions
{
    [EnumDataType(typeof(HomeActivityStyle))]
    public HomeActivityStyle ActivityStyle { get; set; } = HomeActivityStyle.Feed;
}
