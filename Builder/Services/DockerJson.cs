using System.Text.Json;

namespace Builder.Services;

internal static class DockerJson
{
    public static IEnumerable<JsonElement> Objects(string output)
    {
        if (string.IsNullOrWhiteSpace(output))
        {
            yield break;
        }

        if (output.TrimStart().StartsWith('['))
        {
            using var array = JsonDocument.Parse(output);

            foreach (var element in array.RootElement.EnumerateArray())
            {
                yield return element;
            }
        }
        else
        {
            foreach (var line in output.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                using var document = JsonDocument.Parse(line);
                yield return document.RootElement;
            }
        }
    }

    public static string Text(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString() ?? string.Empty
            : string.Empty;
}
