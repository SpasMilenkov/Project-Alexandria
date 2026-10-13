using System.ComponentModel.DataAnnotations;
using Alexandria.Data.Models.Enumerators.Widgets;

namespace Alexandria.Common.Widgets.Options;

public sealed class HomeStatusOptions
{
    [EnumDataType(typeof(HomeStatusStyle))]
    public HomeStatusStyle StatusStyle { get; set; } = HomeStatusStyle.Beacon;
}
