using System.Text.Json;
using System.Text.Json.Serialization;

namespace Alexandria.Dto.Home;

public class HomeBoardChanges
{
    public List<HomeWidgetCreate> Creates { get; set; } = [];
    public List<HomeWidgetUpdate> Updates { get; set; } = [];
    public List<Guid> Deletes { get; set; } = [];
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public JsonElement Preferences { get; set; }
    public bool InitializeMobileLayout { get; set; }
}
