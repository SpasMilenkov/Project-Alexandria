using System.Text.Json.Serialization;

namespace Builder.Models;

// Persisted installer appearance preference (locked decision D28).
// Lowercase key on disk; the explicit name also pins source-gen binding,
// which in the pinned Spectre/STJ alpha is otherwise case-sensitive.
public sealed class UiPreferences
{
    [JsonPropertyName("theme")]
    public string Theme { get; set; } = string.Empty;
}
