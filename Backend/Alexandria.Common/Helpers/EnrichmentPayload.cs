using System.Text.Json;

namespace Alexandria.Common.Helpers;

/// <summary>
/// Helpers for reading <c>FileEnrichment.PayloadJson</c> values written by the Essentia
/// worker. Failure rows are serialized as <c>{"success":false,...}</c>; genre/mood success
/// payloads never carry a <c>success</c> key.
/// </summary>
public static class EnrichmentPayload
{
    /// <summary>
    /// Returns true when the payload is a failure row (<c>success</c> key present and false)
    /// or is unparseable. Shared by the auto-tag sync (winner selection), the backstop
    /// skip-check and the enrichment trigger.
    /// </summary>
    public static bool IsFailure(string json)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            if (document.RootElement.ValueKind != JsonValueKind.Object) return true;
            return document.RootElement.TryGetProperty("success", out var success)
                   && success.ValueKind == JsonValueKind.False;
        }
        catch (JsonException)
        {
            return true;
        }
    }
}