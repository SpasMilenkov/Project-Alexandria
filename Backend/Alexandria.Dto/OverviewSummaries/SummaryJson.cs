using System.Text.Json;
using System.Text.Json.Serialization;

namespace Alexandria.Dto.OverviewSummaries;

public static class SummaryJson
{
    public static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        AllowOutOfOrderMetadataProperties = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public static string Serialize(SummaryPayload payload)
    {
        return JsonSerializer.Serialize(payload, typeof(SummaryPayload), Options);
    }

    public static SummaryPayload Deserialize(string json)
    {
        return JsonSerializer.Deserialize<SummaryPayload>(json, Options)!;
    }
}
