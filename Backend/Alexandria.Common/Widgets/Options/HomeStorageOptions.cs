using System.ComponentModel.DataAnnotations;
using Alexandria.Data.Models.Enumerators.Widgets;

namespace Alexandria.Common.Widgets.Options;

public sealed class HomeStorageOptions
{
    [EnumDataType(typeof(HomeStorageStyle))]
    public HomeStorageStyle StorageStyle { get; set; } = HomeStorageStyle.Meter;
}
