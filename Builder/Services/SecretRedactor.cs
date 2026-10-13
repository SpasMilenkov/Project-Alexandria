using System.Text.RegularExpressions;

namespace Builder.Services;

public sealed class SecretRedactor(Func<IReadOnlyDictionary<string, string>> values)
{
    public string Redact(string text)
    {
        var result = text;

        foreach (var value in values().Where(pair => IsSecret(pair.Key)).Select(pair => pair.Value)
                     .Where(value => value.Length >= 3 && value != "placeholder").Distinct().OrderByDescending(value => value.Length))
        {
            result = result.Replace(value, "[redacted]", StringComparison.Ordinal);
            result = result.Replace(Uri.EscapeDataString(value), "[redacted]", StringComparison.Ordinal);
        }

        result = Regex.Replace(result, @"(?i)(amqps?://)[^\s/@]+(?::[^\s/@]*)?@", "$1[redacted]@");
        result = Regex.Replace(result, @"(?i)(\bBearer\s+)[A-Za-z0-9._~+/-]+=*", "$1[redacted]");
        result = Regex.Replace(result, @"\beyJ[A-Za-z0-9_-]+\.[A-Za-z0-9_-]+\.[A-Za-z0-9_-]+\b", "[redacted]");
        result = Regex.Replace(result, """(?i)(\b(?:password|secret(?:[_ ]key)?|access[_ ]key|token|\w+_(?:PASSWORD|SECRET|TOKEN|KEY))\s*[:=]\s*)(?:"(?:""|\\.|[^"\\])*"|'(?:''|[^'])*'|[^;\s]+)""", "$1[redacted]");

        result = Regex.Replace(result, "\u001b\\[[0-?]*[ -/]*[@-~]", string.Empty);

        return new string(result.Where(character => !char.IsControl(character) || character is '\n' or '\r' or '\t').ToArray());
    }

    private static bool IsSecret(string key) => key.Contains("PASSWORD", StringComparison.OrdinalIgnoreCase) ||
        key.Contains("SECRET", StringComparison.OrdinalIgnoreCase) || key.Contains("TOKEN", StringComparison.OrdinalIgnoreCase) ||
        key.Contains("ACCESS_KEY", StringComparison.OrdinalIgnoreCase) || key.EndsWith("_KEY_ID", StringComparison.OrdinalIgnoreCase);
}
