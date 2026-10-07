using System.ComponentModel.DataAnnotations;
using Alexandria.Data.Models.Enumerators;

namespace Alexandria.Common.Settings.Values;

public class HomeSettingsValue : IValidatableObject
{
    public const int CurrentSchemaVersion = 1;
    public const int MaxWidgetsPerLayout = 32;
    public const int MaxGridRows = 256;
    public const int MaxWidgetHeight = 12;

    public int SchemaVersion { get; set; } = CurrentSchemaVersion;
    public bool UseSeparateMobileLayout { get; set; }
    public HomeLayoutValue Desktop { get; set; } = CreateDefaultLayout();
    public HomeLayoutValue? Mobile { get; set; }

    public static HomeLayoutValue CreateDefaultLayout(int columns = 12) => new()
    {
        Columns = columns,
        Widgets =
        [
            new HomeWidgetValue
            {
                InstanceId = "default-clock", Type = HomeWidgetType.Clock,
                Width = columns == 4 ? 4 : 6, Height = 4
            },
            new HomeWidgetValue
            {
                InstanceId = "default-library", Type = HomeWidgetType.LibraryShortcuts,
                Size = HomeWidgetSize.Large, Y = 4, Width = columns, Height = 6
            }
        ]
    };

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        => HomeSettingsValidation.Validate(this);
}