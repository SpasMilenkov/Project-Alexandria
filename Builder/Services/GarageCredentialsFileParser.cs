using System.Text.RegularExpressions;
using Builder.Models;

namespace Builder.Services;

// Parses the exact contract of init-output/garage-credentials.env written by
// scripts/garage-init/init.sh (locked decision D6). Tolerates comments, blank
// lines, CRLF and whitespace around '='; anything else is a hard error so a
// partial or corrupted file is never merged silently.
public static class GarageCredentialsFileParser
{
    private const string AccessKeyKey = "GARAGE_S3_ACCESS_KEY";
    private const string AccessSecretKey = "GARAGE_S3_SECRET_KEY";
    private const string PreviewKeyIdKey = "GARAGE_PREVIEW_KEY_ID";
    private const string PreviewSecretKey = "GARAGE_PREVIEW_SECRET";
    private const string StreamingKeyIdKey = "GARAGE_STREAMING_KEY_ID";
    private const string StreamingSecretKey = "GARAGE_STREAMING_SECRET";

    private static readonly Regex LinePattern = new(
        @"^\s*(?<key>[A-Za-z_][A-Za-z0-9_]*)\s*=\s*(?<value>.*)\s*$",
        RegexOptions.Compiled);

    public static GarageCredentialSet? Parse(string fileContent)
    {
        if (string.IsNullOrWhiteSpace(fileContent))
        {
            return null;
        }

        var values = new Dictionary<string, string>();

        foreach (var rawLine in fileContent.Replace("\r\n", "\n").Split('\n'))
        {
            var line = rawLine.Trim();

            if (line.Length == 0 || line.StartsWith('#'))
            {
                continue;
            }

            var match = LinePattern.Match(line);

            if (!match.Success)
            {
                throw new FormatException($"Unrecognized line in garage credentials file: '{Truncate(line)}'");
            }

            values[match.Groups["key"].Value] = match.Groups["value"].Value;
        }

        var credentials = new GarageCredentialSet(
            AccessKey: GetValue(values, AccessKeyKey),
            AccessSecret: GetValue(values, AccessSecretKey),
            PreviewKeyId: GetValue(values, PreviewKeyIdKey),
            PreviewSecret: GetValue(values, PreviewSecretKey),
            StreamingKeyId: GetValue(values, StreamingKeyIdKey),
            StreamingSecret: GetValue(values, StreamingSecretKey));

        return credentials.IsComplete ? credentials : null;
    }

    // Maps the parsed key set onto the .env variable names consumed by the
    // compose fragments (master -> api, preview -> workers, streaming -> transpilation)
    public static Dictionary<string, string> ToEnvValues(GarageCredentialSet credentials)
    {
        return new Dictionary<string, string>
        {
            ["GARAGE_S3_ACCESS_KEY"] = credentials.AccessKey,
            ["GARAGE_S3_SECRET_KEY"] = credentials.AccessSecret,
            ["GARAGE_S3_WORKER_ACCESS_KEY"] = credentials.PreviewKeyId,
            ["GARAGE_S3_WORKER_SECRET_KEY"] = credentials.PreviewSecret,
            ["GARAGE_S3_TRANSPILATION_WORKER_ACCESS_KEY"] = credentials.StreamingKeyId,
            ["GARAGE_S3_TRANSPILATION_WORKER_SECRET_KEY"] = credentials.StreamingSecret,
        };
    }

    private static string GetValue(Dictionary<string, string> values, string key)
    {
        return values.TryGetValue(key, out var value) ? value : string.Empty;
    }

    private static string Truncate(string line)
    {
        return line.Length <= 60 ? line : line[..57] + "...";
    }
}
