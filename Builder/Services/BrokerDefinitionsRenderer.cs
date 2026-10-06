using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Builder.Services;

public static class BrokerDefinitionsRenderer
{
    public static string Render(string template, IReadOnlyDictionary<string, string> environment)
    {
        var substitutions = new Dictionary<string, string>
        {
            ["RABBITMQ_USER"] = Required(environment, "RABBITMQ_USER"),
            ["RABBITMQ_PASS"] = Required(environment, "RABBITMQ_PASSWORD"),
            ["RABBITMQ_VHOST"] = Required(environment, "RABBITMQ_VHOST"),
            ["API_RABBITMQ_PASS"] = Required(environment, "API_RABBITMQ_PASSWORD"),
            ["DOCUMENT_WORKER_PASS"] = Required(environment, "DOCUMENT_WORKER_RABBITMQ_PASSWORD"),
            ["MEDIA_WORKER_PASS"] = Required(environment, "MEDIA_WORKER_RABBITMQ_PASSWORD"),
            ["TRANSPILATION_WORKER_PASS"] = Required(environment, "TRANSPILATION_WORKER_RABBITMQ_PASSWORD"),
            ["LYRICS_WORKER_PASS"] = Required(environment, "LYRICS_WORKER_RABBITMQ_PASSWORD"),
            ["MEDIA_METADATA_WORKER_PASS"] = Required(environment, "MEDIA_METADATA_WORKER_RABBITMQ_PASSWORD"),
            ["PLAYLIST_WORKER_PASS"] = Required(environment, "PLAYLIST_WORKER_RABBITMQ_PASSWORD"),
            ["ESSENTIA_WORKER_PASS"] = Required(environment, "ESSENTIA_WORKER_RABBITMQ_PASSWORD"),
        };
        var identities = new Dictionary<string, string>
        {
            ["api_user"] = Required(environment, "API_RABBITMQ_USER"),
            ["document_worker_user"] = Required(environment, "RABBITMQ_DOCUMENT_WORKER"),
            ["media_worker_user"] = Required(environment, "RABBITMQ_MEDIA_WORKER"),
            ["media_transpilation_worker"] = Required(environment, "RABBITMQ_TRANSPILATION_WORKER"),
            ["lyrics_worker_user"] = Required(environment, "RABBITMQ_LYRICS_WORKER"),
            ["media_metadata_worker_user"] = Required(environment, "RABBITMQ_MEDIA_METADATA_WORKER"),
            ["playlist_worker_user"] = Required(environment, "RABBITMQ_PLAYLIST_WORKER"),
            ["essentia_worker_user"] = Required(environment, "RABBITMQ_ESSENTIA_WORKER"),
        };

        using var document = JsonDocument.Parse(template);
        using var output = new MemoryStream();

        using (var writer = new Utf8JsonWriter(output, new JsonWriterOptions { Indented = true }))
        {
            WriteElement(document.RootElement, writer, substitutions, identities);
        }

        var result = Encoding.UTF8.GetString(output.ToArray());
        using var rendered = JsonDocument.Parse(result);
        ValidateIdentities(rendered.RootElement);

        return result;
    }

    private static void WriteElement(
        JsonElement element,
        Utf8JsonWriter writer,
        IReadOnlyDictionary<string, string> substitutions,
        IReadOnlyDictionary<string, string> identities)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                writer.WriteStartObject();

                foreach (var property in element.EnumerateObject())
                {
                    writer.WritePropertyName(property.Name);
                    WriteElement(property.Value, writer, substitutions, identities);
                }

                writer.WriteEndObject();
                break;
            case JsonValueKind.Array:
                writer.WriteStartArray();

                foreach (var item in element.EnumerateArray())
                {
                    WriteElement(item, writer, substitutions, identities);
                }

                writer.WriteEndArray();
                break;
            case JsonValueKind.String:
                var value = element.GetString()!;

                if (identities.TryGetValue(value, out var identity))
                {
                    value = identity;
                }
                else
                {
                    value = Regex.Replace(value, @"\$\{(?<key>[^{}]*)\}", match =>
                    {
                        var key = match.Groups["key"].Value;

                        if (!substitutions.TryGetValue(key, out var replacement))
                            throw new InvalidOperationException($"Broker definitions contain unsupported substitution '{key}'.");

                        return replacement;
                    });
                }

                writer.WriteStringValue(value);
                break;
            default:
                element.WriteTo(writer);
                break;
        }
    }

    private static string Required(IReadOnlyDictionary<string, string> environment, string key)
    {
        if (!environment.TryGetValue(key, out var value) || string.IsNullOrWhiteSpace(value) || value.Equals("placeholder", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException($"Broker definitions require nonempty '{key}' before any files are written.");

        if (value.Any(char.IsControl))
            throw new InvalidOperationException($"Broker definitions value '{key}' contains an unsupported control character.");

        return value;
    }

    private static void ValidateIdentities(JsonElement definitions)
    {
        var users = definitions.GetProperty("users").EnumerateArray()
            .Select(user => user.GetProperty("name").GetString()!).ToArray();

        if (users.Length != users.Distinct(StringComparer.Ordinal).Count())
            throw new InvalidOperationException("Broker definitions contain duplicate user identities. Review the configured consumer usernames.");

        foreach (var permission in definitions.GetProperty("permissions").EnumerateArray())
        {
            if (!users.Contains(permission.GetProperty("user").GetString(), StringComparer.Ordinal))
                throw new InvalidOperationException("Broker definitions grant permissions to an undefined user identity.");
        }
    }
}
