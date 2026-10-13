using System.ComponentModel.DataAnnotations;
using Alexandria.Data.Models.Enumerators.Widgets;

namespace Alexandria.Common.Widgets.Options;

public sealed class HomeCountdownOptions
{
    [EnumDataType(typeof(HomeCountdownStyle))]
    public HomeCountdownStyle CountdownStyle { get; set; }

    [Required, StringLength(120)]
    public string CountdownLabel { get; set; } = "Countdown";

    public DateOnly? CountdownTarget { get; set; }
}
