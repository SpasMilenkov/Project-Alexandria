using System.ComponentModel.DataAnnotations;
using Alexandria.Data.Models.Enumerators.Widgets;

namespace Alexandria.Common.Widgets.Options;

public sealed class HomeFocusOptions
{
    [EnumDataType(typeof(HomeFocusStyle))]
    public HomeFocusStyle FocusStyle { get; set; }

    [Range(1, 180)]
    public int FocusMinutes { get; set; } = 25;

    [Range(1, 60)]
    public int ShortBreakMinutes { get; set; } = 5;

    [Range(1, 120)]
    public int LongBreakMinutes { get; set; } = 15;

    [Range(1, 8)]
    public int FocusSessions { get; set; } = 4;
}
