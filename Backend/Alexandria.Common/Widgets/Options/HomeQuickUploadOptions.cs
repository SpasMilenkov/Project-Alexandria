using System.ComponentModel.DataAnnotations;
using Alexandria.Data.Models.Enumerators.Widgets;

namespace Alexandria.Common.Widgets.Options;

public sealed class HomeQuickUploadOptions
{
    [EnumDataType(typeof(HomeQuickUploadStyle))]
    public HomeQuickUploadStyle QuickUploadStyle { get; set; } = HomeQuickUploadStyle.DropZone;
}
