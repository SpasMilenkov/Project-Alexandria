using System.ComponentModel.DataAnnotations;
using Alexandria.Data.Models.Enumerators.Widgets;

namespace Alexandria.Common.Widgets.Options;

public sealed class HomeDailyCoverOptions
{
    [EnumDataType(typeof(HomeDailyCoverStyle))]
    public HomeDailyCoverStyle DailyCoverStyle { get; set; }
}
