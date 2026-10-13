using System.Text.Json;

namespace Alexandria.Dto.Home;

public sealed record HomeWidgetUpdate(Guid Id, JsonElement Changes);
