using System.Text.Json;

namespace Alexandria.Services.User.Settings;

public static class TypedSettingAccessor
{
    private static readonly JsonSerializerOptions _opts = new()
    {
        PropertyNameCaseInsensitive = true
    };

    // Deserialize raw JSON string → T, returning default(T) on null/empty
    public static T GetValue<T>(string? rawJson) where T : new()
    {
        if (string.IsNullOrWhiteSpace(rawJson))
            return new T();

        try
        {
            return JsonSerializer.Deserialize<T>(rawJson, _opts) ?? new T();
        }
        catch (JsonException)
        {
            return new T();
        }
    }

    public static string SetValue<T>(T value)
        => JsonSerializer.Serialize(value, _opts);
}