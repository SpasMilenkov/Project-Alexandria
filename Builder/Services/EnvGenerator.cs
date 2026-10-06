using System.Data.Common;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Builder.Models;

namespace Builder.Services;

// Builds the complete .env content for the generated stack. Ordering and
// section comments are stable so regenerated files stay diff-friendly.
public static class EnvGenerator
{
    private const string PendingSecretMarker = "placeholder";
    private static readonly Regex KeyPattern = new("^[A-Za-z_][A-Za-z0-9_]*$", RegexOptions.CultureInvariant);
    private static readonly HashSet<string> GarageKeySlots =
    [
        "GARAGE_S3_ACCESS_KEY", "GARAGE_S3_SECRET_KEY",
        "GARAGE_S3_WORKER_ACCESS_KEY", "GARAGE_S3_WORKER_SECRET_KEY",
        "GARAGE_S3_TRANSPILATION_WORKER_ACCESS_KEY", "GARAGE_S3_TRANSPILATION_WORKER_SECRET_KEY",
    ];

    public static Dictionary<string, string> BuildValues(
        FeatureSelection features,
        IReadOnlyDictionary<string, string> credentials,
        AdminAccountInput admin,
        GarageCredentialSet? garageCredentials)
    {
        var values = new Dictionary<string, string>
        {
            // Database
            ["DB_PORT"] = "5432",
            ["DB_NAME"] = "alexandria_dev",
            ["DB_USERNAME"] = "alexandria_user",
            ["DB_PASSWORD"] = credentials.GetValueOrDefault("DB_PASSWORD", string.Empty),

            // Admin account (chosen by the user in the wizard, D8)
            ["ADMIN_EMAIL"] = admin.Email,
            ["ADMIN_USERNAME"] = admin.Username,
            ["ADMIN_PASSWORD"] = admin.Password,

            // Public URLs
            ["BASE_URL"] = string.Empty,
            ["GARAGE_S3_PUBLIC_ENDPOINT"] = string.Empty,

            // RabbitMQ
            ["RABBITMQ_USER"] = credentials.GetValueOrDefault("RABBITMQ_USER", string.Empty),
            ["RABBITMQ_PASSWORD"] = credentials.GetValueOrDefault("RABBITMQ_PASSWORD", string.Empty),
            ["API_RABBITMQ_USER"] = credentials.GetValueOrDefault("API_RABBITMQ_USER", string.Empty),
            ["API_RABBITMQ_PASSWORD"] = credentials.GetValueOrDefault("API_RABBITMQ_PASSWORD", string.Empty),
            ["RABBITMQ_DOCUMENT_WORKER"] = credentials.GetValueOrDefault("RABBITMQ_DOCUMENT_WORKER", string.Empty),
            ["DOCUMENT_WORKER_RABBITMQ_PASSWORD"] = credentials.GetValueOrDefault("DOCUMENT_WORKER_RABBITMQ_PASSWORD", string.Empty),
            ["RABBITMQ_MEDIA_WORKER"] = credentials.GetValueOrDefault("RABBITMQ_MEDIA_WORKER", string.Empty),
            ["MEDIA_WORKER_RABBITMQ_PASSWORD"] = credentials.GetValueOrDefault("MEDIA_WORKER_RABBITMQ_PASSWORD", string.Empty),
            ["RABBITMQ_TRANSPILATION_WORKER"] = credentials.GetValueOrDefault("RABBITMQ_TRANSPILATION_WORKER", string.Empty),
            ["TRANSPILATION_WORKER_RABBITMQ_PASSWORD"] = credentials.GetValueOrDefault("TRANSPILATION_WORKER_RABBITMQ_PASSWORD", string.Empty),
            ["RABBITMQ_MEDIA_METADATA_WORKER"] = credentials.GetValueOrDefault("RABBITMQ_MEDIA_METADATA_WORKER", string.Empty),
            ["MEDIA_METADATA_WORKER_RABBITMQ_PASSWORD"] = credentials.GetValueOrDefault("MEDIA_METADATA_WORKER_RABBITMQ_PASSWORD", string.Empty),
            ["RABBITMQ_LYRICS_WORKER"] = credentials.GetValueOrDefault("RABBITMQ_LYRICS_WORKER", string.Empty),
            ["LYRICS_WORKER_RABBITMQ_PASSWORD"] = credentials.GetValueOrDefault("LYRICS_WORKER_RABBITMQ_PASSWORD", string.Empty),
            ["RABBITMQ_PLAYLIST_WORKER"] = credentials.GetValueOrDefault("RABBITMQ_PLAYLIST_WORKER", "playlist_worker_user"),
            ["PLAYLIST_WORKER_RABBITMQ_PASSWORD"] = credentials.GetValueOrDefault("PLAYLIST_WORKER_RABBITMQ_PASSWORD", string.Empty),
            ["RABBITMQ_ESSENTIA_WORKER"] = credentials.GetValueOrDefault("RABBITMQ_ESSENTIA_WORKER", "essentia_worker_user"),
            ["ESSENTIA_WORKER_RABBITMQ_PASSWORD"] = credentials.GetValueOrDefault("ESSENTIA_WORKER_RABBITMQ_PASSWORD", string.Empty),
            ["RABBITMQ_VHOST"] = credentials.GetValueOrDefault("RABBITMQ_VHOST", "alexandria"),

            // Garage cluster — application keys stay pending until the
            // garage-init merge (D6)
            ["GARAGE_RPC_SECRET"] = credentials.GetValueOrDefault("GARAGE_RPC_SECRET", string.Empty),
            ["GARAGE_CAPACITY"] = "10G",
            ["GARAGE_CAPACITY_CHANGE_REQUESTED"] = "false",
            ["GARAGE_ADMIN_TOKEN"] = credentials.GetValueOrDefault("GARAGE_ADMIN_TOKEN", string.Empty),
            ["GARAGE_S3_METRICS_TOKEN"] = credentials.GetValueOrDefault("GARAGE_S3_METRICS_TOKEN",
                credentials.GetValueOrDefault("GARAGE_METRICS_TOKEN", string.Empty)),
            ["GARAGE_S3_ACCESS_KEY"] = PendingSecretMarker,
            ["GARAGE_S3_SECRET_KEY"] = PendingSecretMarker,
            ["GARAGE_S3_WORKER_ACCESS_KEY"] = PendingSecretMarker,
            ["GARAGE_S3_WORKER_SECRET_KEY"] = PendingSecretMarker,
            ["GARAGE_S3_TRANSPILATION_WORKER_ACCESS_KEY"] = PendingSecretMarker,
            ["GARAGE_S3_TRANSPILATION_WORKER_SECRET_KEY"] = PendingSecretMarker,

            // Application secrets
            ["JWT_SECRET"] = credentials.GetValueOrDefault("JWT_SECRET", string.Empty),
            ["JWT_ISSUER"] = credentials.GetValueOrDefault("JWT_ISSUER", string.Empty),
            ["JWT_AUDIENCE"] = credentials.GetValueOrDefault("JWT_AUDIENCE", string.Empty),
            ["CSRF_SECRET"] = credentials.GetValueOrDefault("CSRF_SECRET", string.Empty),

            // The reviewed endpoint supplies browser origins before any artifacts are written.
            ["CORS_ALLOWED_ORIGINS__0"] = string.Empty,
            ["CORS_ORIGIN"] = string.Empty,

            // Frontend / feature switches derived from the wizard selection
            ["PBKDF2_ITERATION_COUNT"] = "800000",
            // Required unconditionally: the postgres container seeds its
            // exporter login whether or not monitoring is enabled
            ["POSTGRES_EXPORTER_PASSWORD"] = credentials.GetValueOrDefault("POSTGRES_EXPORTER_PASSWORD", string.Empty),
        };

        var capabilities = ServiceCatalog.Resolve(features);

        foreach (var (key, value) in capabilities.EnvironmentFlags)
        {
            values[key] = value;
        }

        if (capabilities.MonitoringEnabled)
        {
            values["GRAFANA_USER"] = credentials.GetValueOrDefault("GRAFANA_USER", "admin");
            values["GRAFANA_PASSWORD"] = credentials.GetValueOrDefault("GRAFANA_PASSWORD",
                credentials.GetValueOrDefault("GRAFANA_ADMIN_PASSWORD", string.Empty));
        }

        if (garageCredentials is not null)
        {
            foreach (var (key, value) in GarageCredentialsFileParser.ToEnvValues(garageCredentials))
            {
                values[key] = value;
            }
        }

        return values;
    }

    public static Dictionary<string, string> Parse(string content)
    {
        return ParseLines(content).Where(line => line.Key is not null)
            .ToDictionary(line => line.Key!, line => line.Value!, StringComparer.Ordinal);
    }

    public static string Render(IDictionary<string, string> values, string? existingEnvContent = null)
    {
        foreach (var (key, value) in values)
        {
            ValidateKey(key, 0);
            ValidateValue(value, key, 0);
        }

        var builder = new StringBuilder();
        var written = new HashSet<string>(StringComparer.Ordinal);

        if (!string.IsNullOrEmpty(existingEnvContent))
        {
            var lines = ParseLines(existingEnvContent);

            if (existingEnvContent.EndsWith('\n')) lines.RemoveAt(lines.Count - 1);

            foreach (var line in lines)
            {
                if (line.Key is null)
                {
                    builder.AppendLine(line.Raw);
                    continue;
                }

                var value = values.TryGetValue(line.Key, out var replacement) ? replacement : line.Value!;
                builder.Append(line.Key).Append('=').Append(Encode(value)).AppendLine(line.Comment);
                written.Add(line.Key);
            }

            if (values.Keys.Any(key => !written.Contains(key))) builder.AppendLine();
        }
        else
        {
            builder.AppendLine("# Alexandria - generated environment configuration");
            builder.AppendLine("# Contains installation secrets. Do not commit or share.");
            builder.AppendLine();
        }

        AppendSection(builder, "Database", values, written, "DB_");
        AppendSection(builder, "Admin account", values, written, "ADMIN_");
        AppendSection(builder, "Public URLs", values, written, "BASE_URL", "GARAGE_S3_PUBLIC_ENDPOINT");
        AppendSection(builder, "RabbitMQ", values, written, "RABBITMQ_", "API_RABBITMQ_", "DOCUMENT_WORKER_RABBITMQ_", "MEDIA_WORKER_RABBITMQ_", "TRANSPILATION_WORKER_RABBITMQ_", "MEDIA_METADATA_WORKER_RABBITMQ_", "LYRICS_WORKER_RABBITMQ_", "PLAYLIST_WORKER_RABBITMQ_", "ESSENTIA_WORKER_RABBITMQ_");
        AppendSection(builder, "Garage S3 storage", values, written, "GARAGE_");
        AppendSection(builder, "Application secrets", values, written, "JWT_", "CSRF_");
        AppendSection(builder, "CORS", values, written, "CORS_");
        AppendSection(builder, "Frontend and feature switches", values, written, "PBKDF2_", "STREAMING_", "LYRICS_", "AUTOTAGGING_");
        AppendSection(builder, "Monitoring", values, written, "GRAFANA_", "POSTGRES_EXPORTER_");
        AppendSection(builder, "Additional settings", values, written, string.Empty);

        return builder.ToString();
    }

    public static void WriteFile(string path, IDictionary<string, string> values, string? existingEnvContent = null)
    {
        existingEnvContent ??= File.Exists(path) ? File.ReadAllText(path) : null;
        var content = Render(values, existingEnvContent);

        Parse(content);
        ProtectedFile.WriteAtomic(path, content);
    }

    public static Dictionary<string, string> MergeWithExisting(
        IDictionary<string, string> generated,
        string? existingEnvContent)
    {
        var merged = new Dictionary<string, string>(generated, StringComparer.Ordinal);

        if (existingEnvContent is null)
        {
            return merged;
        }

        foreach (var (key, value) in Parse(existingEnvContent))
        {
            if (GarageKeySlots.Contains(key) && IsPending(value) &&
                generated.TryGetValue(key, out var authoritative) && !IsPending(authoritative))
            {
                continue;
            }

            merged[key] = value;
        }

        return merged;
    }

    public static string BuildDatabaseConnectionString(IDictionary<string, string> values)
    {
        var port = int.Parse(values["DB_PORT"], NumberStyles.None, CultureInfo.InvariantCulture);
        ArgumentOutOfRangeException.ThrowIfLessThan(port, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(port, 65535);

        var connection = new DbConnectionStringBuilder
        {
            ["Host"] = "postgres",
            ["Port"] = port,
            ["Database"] = values["DB_NAME"],
            ["Username"] = values["DB_USERNAME"],
            ["Password"] = values["DB_PASSWORD"],
            ["GssEncryptionMode"] = "Disable",
        };

        return connection.ConnectionString;
    }

    public static string BuildAmqpUri(string host, string user, string password, string virtualHost, int port = 5672)
    {
        if (Uri.CheckHostName(host) == UriHostNameType.Unknown)
        {
            throw new ArgumentException("The AMQP hostname must be a DNS name or IP address.", nameof(host));
        }

        ArgumentOutOfRangeException.ThrowIfLessThan(port, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(port, 65535);

        var authority = host.Contains(':') ? $"[{host.Trim('[', ']')}]" : host;

        return $"amqp://{Uri.EscapeDataString(user)}:{Uri.EscapeDataString(password)}@{authority}:{port}/{Uri.EscapeDataString(virtualHost)}";
    }

    private static void AppendSection(
        StringBuilder builder,
        string title,
        IDictionary<string, string> values,
        HashSet<string> written,
        params string[] prefixes)
    {
        var keys = values.Keys.Where(key => !written.Contains(key) &&
            prefixes.Any(prefix => key.StartsWith(prefix, StringComparison.Ordinal))).ToArray();

        if (keys.Length == 0)
        {
            return;
        }

        builder.AppendLine($"# --- {title} ---");

        foreach (var key in keys)
        {
            builder.Append(key).Append('=').AppendLine(Encode(values[key]));
            written.Add(key);
        }

        builder.AppendLine();
    }

    private static string Encode(string value)
    {
        var precedingSlashes = 0;
        var requiresDoubleQuotes = false;

        foreach (var character in value)
        {
            if (character == '\'')
            {
                requiresDoubleQuotes |= precedingSlashes % 2 != 0;
            }

            precedingSlashes = character == '\\' ? precedingSlashes + 1 : 0;
        }

        requiresDoubleQuotes |= precedingSlashes % 2 != 0;

        if (requiresDoubleQuotes)
        {
            return "\"" + value.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("$", "$$") + "\"";
        }

        return "'" + value.Replace("'", "\\'") + "'";
    }

    private static List<EnvLine> ParseLines(string content)
    {
        var result = new List<EnvLine>();
        var seen = new Dictionary<string, string>(StringComparer.Ordinal);
        var lines = content.Replace("\r\n", "\n").Split('\n');

        for (var index = 0; index < lines.Length; index++)
        {
            var raw = lines[index];
            var line = raw.Trim();
            var lineNumber = index + 1;

            if (line.Length == 0 || line.StartsWith('#'))
            {
                result.Add(new EnvLine(raw));
                continue;
            }

            if (line.StartsWith("export ", StringComparison.Ordinal))
            {
                line = line[7..].TrimStart();
            }

            var separator = line.IndexOf('=');

            if (separator <= 0)
            {
                throw Syntax(lineNumber, null, "Expected NAME=value; unset variables and multiline values are unsupported.");
            }

            var key = line[..separator].Trim();
            ValidateKey(key, lineNumber);

            var input = line[(separator + 1)..].TrimStart();
            string value;
            string? comment = null;

            if (input.StartsWith('\'') || input.StartsWith('"'))
            {
                var quote = input[0];
                var slashes = 0;
                var end = -1;

                for (var position = 1; position < input.Length; position++)
                {
                    if (input[position] == quote && slashes % 2 == 0)
                    {
                        end = position;
                        break;
                    }

                    slashes = input[position] == '\\' ? slashes + 1 : 0;
                }

                if (end < 0)
                {
                    throw Syntax(lineNumber, key, "Unterminated quoted value; multiline values are unsupported.");
                }

                var remainder = input[(end + 1)..].TrimStart();

                if (remainder.Length > 0 && !remainder.StartsWith('#'))
                {
                    throw Syntax(lineNumber, key, "Unexpected content after a quoted value.");
                }

                comment = remainder.Length > 0 ? " " + remainder : null;
                var quoted = input[1..end];
                value = quote == '\'' ? quoted.Replace("\\'", "'") : DecodeDoubleQuoted(quoted, key, lineNumber);

                if (quote == '"')
                {
                    value = DecodeInterpolation(value, key, lineNumber);
                }
            }
            else
            {
                var commentAt = -1;

                for (var position = 0; position < input.Length; position++)
                {
                    if (input[position] == '#' && (position == 0 || char.IsWhiteSpace(input[position - 1])))
                    {
                        commentAt = position;
                        break;
                    }
                }

                if (commentAt >= 0)
                {
                    comment = " " + input[commentAt..];
                    input = input[..commentAt];
                }

                value = DecodeInterpolation(input.TrimEnd(), key, lineNumber);
            }

            ValidateValue(value, key, lineNumber);

            if (seen.TryGetValue(key, out var earlier))
            {
                if (earlier != value)
                {
                    throw Syntax(lineNumber, key, "Conflicting duplicate variable; resolve it before rewriting the environment file.");
                }

                if (comment is not null)
                {
                    result.Add(new EnvLine(comment.TrimStart()));
                }

                continue;
            }

            seen.Add(key, value);
            result.Add(new EnvLine(raw, key, value, comment));
        }

        return result;
    }

    private static string DecodeDoubleQuoted(string value, string key, int line)
    {
        var decoded = new StringBuilder();

        for (var index = 0; index < value.Length; index++)
        {
            if (value[index] != '\\')
            {
                decoded.Append(value[index]);
                continue;
            }

            if (++index >= value.Length || value[index] is not ('\\' or '"'))
            {
                throw Syntax(line, key, "Unsupported double-quoted escape; use a single-quoted literal value.");
            }

            decoded.Append(value[index]);
        }

        return decoded.ToString();
    }

    private static string DecodeInterpolation(string value, string key, int line)
    {
        var decoded = new StringBuilder();

        for (var index = 0; index < value.Length; index++)
        {
            if (value[index] == '$' && index + 1 < value.Length)
            {
                if (value[index + 1] == '$')
                {
                    decoded.Append('$');
                    index++;
                    continue;
                }

                if (value[index + 1] == '{' || char.IsAsciiLetter(value[index + 1]) || value[index + 1] == '_')
                {
                    throw Syntax(line, key, "Environment interpolation is unsupported; quote the resolved literal with single quotes.");
                }
            }

            decoded.Append(value[index]);
        }

        return decoded.ToString();
    }

    private static void ValidateKey(string key, int line)
    {
        if (!KeyPattern.IsMatch(key))
        {
            throw Syntax(line, null, "Variable names must use ASCII letters, digits, and underscores, beginning with a letter or underscore.");
        }
    }

    private static void ValidateValue(string value, string key, int line)
    {
        if (value is null || value.Any(character => char.IsControl(character) || character is '\u2028' or '\u2029'))
        {
            throw Syntax(line, key, "Control characters and multiline values are unsupported.");
        }

        try
        {
            new UTF8Encoding(false, true).GetByteCount(value);
        }
        catch (EncoderFallbackException)
        {
            throw Syntax(line, key, "Invalid Unicode value.");
        }
    }

    private static bool IsPending(string value)
    {
        return string.IsNullOrWhiteSpace(value) || value.Equals(PendingSecretMarker, StringComparison.OrdinalIgnoreCase);
    }

    private static FormatException Syntax(int line, string? key, string reason)
    {
        var location = line > 0 ? $" on line {line}" : string.Empty;
        var variable = key is null ? string.Empty : $" ({key})";

        return new FormatException($"Unsupported dotenv syntax{location}{variable}. {reason}");
    }

    private sealed record EnvLine(string Raw, string? Key = null, string? Value = null, string? Comment = null);
}
