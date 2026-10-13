using System.Text.Json;
using Alexandria.Data.Models.Enumerators.Widgets;

namespace Alexandria.Dto.Home;

public sealed record HomeWidgetDto(
    Guid Id, HomeLayout Layout, HomeWidgetType Type,
    int X, int Y, int Width, int Height, bool? ShowTitle,
    int OptionsVersion, JsonElement Options);
