using System.Text;
using System.Text.RegularExpressions;
using Builder.Models;

namespace Builder.Services;

// Builds the complete .env content for the generated stack. Ordering and
// section comments are stable so regenerated files stay diff-friendly.
// Existing user values always win over generated ones (locked decision D14).
public static class EnvGenerator
{
    private const string PendingSecretMarker = "placeholder";

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
            ["BASE_URL"] = "https://localhost",
            ["GARAGE_S3_PUBLIC_ENDPOINT"] = "https://localhost/s3/",

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
            ["ESSENTIA_WORKER_RABBITMQ_PASSWORD"] = credentials.GetValueOrDefault("ESSENTIA_WORKER_RABBITMQ_PASSWORD", string.Empty),
            ["RABBITMQ_VHOST"] = credentials.GetValueOrDefault("RABBITMQ_VHOST", "alexandria"),

            // Garage cluster — application keys stay pending until the
            // garage-init merge (D6)
            ["GARAGE_RPC_SECRET"] = credentials.GetValueOrDefault("GARAGE_RPC_SECRET", string.Empty),
            ["GARAGE_ADMIN_TOKEN"] = credentials.GetValueOrDefault("GARAGE_ADMIN_TOKEN", string.Empty),
            ["GARAGE_S3_METRICS_TOKEN"] = credentials.GetValueOrDefault("GARAGE_METRICS_TOKEN", string.Empty),
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

            // CORS — local defaults; the browser origin must match nginx/vite
            ["CORS_ALLOWED_ORIGINS__0"] = "http://localhost:5173",
            ["CORS_ALLOWED_ORIGINS__1"] = "https://localhost",
            ["CORS_ORIGIN"] = "http://localhost:5173",

            // Frontend / feature switches derived from the wizard selection
            ["PBKDF2_ITERATION_COUNT"] = "800000",
            ["STREAMING_ENABLED"] = features.IsEnabled(FeatureCatalog.AdaptiveStreaming.Id) ? "true" : "false",
            ["LYRICS_ENABLED"] = features.IsEnabled(FeatureCatalog.Lyrics.Id) ? "true" : "false",
            ["AUTOTAGGING_ENABLED"] = features.IsEnabled(FeatureCatalog.Autotagging.Id) ? "true" : "false",

            // Required unconditionally: the postgres container seeds its
            // exporter login whether or not monitoring is enabled
            ["POSTGRES_EXPORTER_PASSWORD"] = credentials.GetValueOrDefault("POSTGRES_EXPORTER_PASSWORD", string.Empty),
        };

        if (features.IsEnabled(FeatureCatalog.Monitoring.Id))
        {
            values["GRAFANA_USER"] = credentials.GetValueOrDefault("GRAFANA_USER", "admin");
            values["GRAFANA_PASSWORD"] = credentials.GetValueOrDefault("GRAFANA_ADMIN_PASSWORD", string.Empty);
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

    public static string Render(Dictionary<string, string> values)
    {
        var builder = new StringBuilder();

        builder.AppendLine("# ============================================================");
        builder.AppendLine("# Alexandria - generated environment configuration");
        builder.AppendLine("# Created by the Alexandria Installer. Secrets live only here");
        builder.AppendLine("# and in garage.toml / prometheus/secrets. Do not commit.");
        builder.AppendLine("# ============================================================");
        builder.AppendLine();

        AppendSection(builder, "Database", values, "DB_");
        AppendSection(builder, "Admin account", values, "ADMIN_");
        AppendSection(builder, "Public URLs", values, "BASE_URL", "GARAGE_S3_PUBLIC_ENDPOINT");
        AppendSection(builder, "RabbitMQ", values, "RABBITMQ_", "API_RABBITMQ_", "DOCUMENT_WORKER_RABBITMQ_", "MEDIA_WORKER_RABBITMQ_", "TRANSPILATION_WORKER_RABBITMQ_", "MEDIA_METADATA_WORKER_RABBITMQ_", "LYRICS_WORKER_RABBITMQ_", "ESSENTIA_WORKER_RABBITMQ_");
        AppendSection(builder, "Garage S3 storage", values, "GARAGE_");
        AppendSection(builder, "Application secrets", values, "JWT_", "CSRF_");
        AppendSection(builder, "CORS", values, "CORS_");
        AppendSection(builder, "Frontend and feature switches", values, "PBKDF2_", "STREAMING_", "LYRICS_", "AUTOTAGGING_");
        AppendSection(builder, "Monitoring", values, "GRAFANA_", "POSTGRES_EXPORTER_");

        return builder.ToString();
    }

    public static void WriteFile(string path, Dictionary<string, string> values)
    {
        File.WriteAllText(path, Render(values));

        // Owner-only access: this file holds every secret of the install
        if (!OperatingSystem.IsWindows())
        {
            File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite);
        }
    }

    // D14: existing entries are never overwritten; missing ones are filled in
    public static Dictionary<string, string> MergeWithExisting(
        Dictionary<string, string> generated,
        string? existingEnvContent)
    {
        if (string.IsNullOrWhiteSpace(existingEnvContent))
        {
            return new Dictionary<string, string>(generated);
        }

        var merged = new Dictionary<string, string>(generated);

        foreach (var rawLine in existingEnvContent.Replace("\r\n", "\n").Split('\n'))
        {
            var line = rawLine.Trim();

            if (line.Length == 0 || line.StartsWith('#'))
            {
                continue;
            }

            var separatorIndex = line.IndexOf('=');

            if (separatorIndex <= 0)
            {
                continue;
            }

            var key = line[..separatorIndex].Trim();
            var value = line[(separatorIndex + 1)..].Trim();

            // D14: an existing non-empty user value always wins; generated
            // entries only fill gaps
            if (!string.IsNullOrWhiteSpace(value))
            {
                merged[key] = value;
            }
        }

        return merged;
    }

    private static void AppendSection(
        StringBuilder builder,
        string title,
        Dictionary<string, string> values,
        params string[] prefixes)
    {
        builder.AppendLine($"# --- {title} ---");

        var writtenAny = false;

        foreach (var key in values.Keys)
        {
            if (!prefixes.Any(key.StartsWith))
            {
                continue;
            }

            builder.AppendLine($"{key}={values[key]}");
            writtenAny = true;
        }

        if (!writtenAny)
        {
            builder.AppendLine("# (not used by the selected features)");
        }

        builder.AppendLine();
    }
}
