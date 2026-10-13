using System.ComponentModel.DataAnnotations;
using Alexandria.Common.Settings.Values;
using Alexandria.Data.Models.Enumerators.Widgets;

namespace Alexandria.Common.Widgets.Options;

public sealed class HomeClockOptions
{
    [Required, StringLength(HomeTimeZones.MaxIdentifierLength)]
    public string TimeZone { get; set; } = HomeTimeZones.LocalId;

    [EnumDataType(typeof(HomeClockStyle))]
    public HomeClockStyle ClockStyle { get; set; } = HomeClockStyle.Minimal;

    [EnumDataType(typeof(HomeClockHourFormat))]
    public HomeClockHourFormat HourFormat { get; set; } = HomeClockHourFormat.DeviceDefault;

    public bool ShowSeconds { get; set; }

    [EnumDataType(typeof(HomeHorizontalAlignment))]
    public HomeHorizontalAlignment HorizontalAlignment { get; set; } = HomeHorizontalAlignment.Left;

    [EnumDataType(typeof(HomeVerticalAlignment))]
    public HomeVerticalAlignment VerticalAlignment { get; set; } = HomeVerticalAlignment.Middle;

    [EnumDataType(typeof(HomeHorizontalAlignment))]
    public HomeHorizontalAlignment TextAlignment { get; set; } = HomeHorizontalAlignment.Left;

    [EnumDataType(typeof(HomeVerticalAlignment))]
    public HomeVerticalAlignment TextVerticalAlignment { get; set; } = HomeVerticalAlignment.Bottom;
}
