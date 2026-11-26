using System.Text.Json;

namespace Builder.Deployment;

// Reads the JSON array the worker's --probe-gpu prints, one entry per device asked about.
public static class VideoGpuTestReport
{
    // Null when the output is not the probe's report, for example when the container never started.
    public static IReadOnlyList<VideoGpuTestResult>? Parse(string output)
    {
        var start = output.IndexOf('[', StringComparison.Ordinal);

        if (start < 0) return null;

        try
        {
            using var document = JsonDocument.Parse(output[start..]);

            if (document.RootElement.ValueKind != JsonValueKind.Array) return null;

            return document.RootElement.EnumerateArray()
                .Where(entry => entry.ValueKind == JsonValueKind.Object && Text(entry, "device") is not null)
                .Select(entry => new VideoGpuTestResult(
                    Text(entry, "device")!,
                    entry.TryGetProperty("usable", out var usable) && usable.ValueKind == JsonValueKind.True,
                    Text(entry, "driver"),
                    Text(entry, "detail")))
                .ToArray();
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string? Text(JsonElement entry, string name) =>
        entry.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;
}
