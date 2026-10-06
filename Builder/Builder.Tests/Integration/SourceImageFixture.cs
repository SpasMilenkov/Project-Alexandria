using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Builder.Services;

namespace Builder.Tests.Integration;

internal sealed class SourceImageFixture
{
    internal const string EnvironmentVariable = "ALEXANDRIA_BUILDER_SOURCE_IMAGES";

    private static readonly HashSet<string> ImageNames =
    [
        "api", "document-worker", "media-worker", "transpilation-worker", "media-metadata-worker",
        "playlist-worker", "lyrics-worker", "frontend", "nginx", "garage-init", "essentia",
    ];

    private readonly Dictionary<string, (string Name, string Id)> _images = new(StringComparer.Ordinal);

    private SourceImageFixture() { }

    public static SourceImageFixture? FromEnvironment()
    {
        var path = Environment.GetEnvironmentVariable(EnvironmentVariable);

        return path is null ? null : Load(path);
    }

    internal static SourceImageFixture Load(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            throw new InvalidOperationException($"{EnvironmentVariable} must name an existing JSON build-results file.");

        using var parsed = JsonDocument.Parse(File.ReadAllText(path));
        var fixture = new SourceImageFixture();

        if (parsed.RootElement.ValueKind != JsonValueKind.Array || parsed.RootElement.GetArrayLength() == 0)
            throw new InvalidOperationException("The source-image manifest must contain a nonempty array of successful build results.");

        foreach (var entry in parsed.RootElement.EnumerateArray())
        {
            if (entry.ValueKind != JsonValueKind.Object)
                throw new InvalidOperationException("Each source-image build result must be an object.");

            var properties = entry.EnumerateObject().Select(property => property.Name).ToArray();

            if (properties.Distinct(StringComparer.Ordinal).Count() != properties.Length)
                throw new InvalidOperationException("Source-image build results cannot contain duplicate properties.");

            var name = RequiredString(entry, "name");
            var image = RequiredString(entry, "image");
            var id = RequiredString(entry, "image_id");

            if (!ImageNames.Contains(name) || !Regex.IsMatch(image, @"^[a-zA-Z0-9][a-zA-Z0-9._/:@-]*$") ||
                !Regex.IsMatch(id, @"^sha256:[0-9a-f]{64}$") ||
                !entry.TryGetProperty("exit_code", out var exitCode) || exitCode.ValueKind != JsonValueKind.Number ||
                !exitCode.TryGetInt32(out var code) || code != 0)
                throw new InvalidOperationException("Each source image must have a recognized name, valid image reference, immutable image ID and successful exit_code.");

            if (!fixture._images.TryAdd(name, (image, id)))
                throw new InvalidOperationException("The source-image manifest contains a duplicate service: " + name);
        }

        return fixture;
    }

    public string Image(string service) => RequiredImage(service).Id;

    public void ValidateImages(IEnumerable<string> services, Func<IReadOnlyList<string>, ProcessResult> docker)
    {
        foreach (var service in services.Distinct(StringComparer.Ordinal))
        {
            var image = RequiredImage(service);
            var inspected = docker(["image", "inspect", "--format", "{{.Id}}", image.Name]);

            if (!inspected.Succeeded || inspected.StandardOutput.Trim() != image.Id)
                throw new InvalidOperationException($"Source image '{service}' is unavailable or its named image no longer matches the recorded immutable image ID. Source mode never falls back, pulls or builds.");
        }
    }

    public void AppendOverride(StringBuilder text, string service)
    {
        text.AppendLine($$"""
              {{service}}:
                build: !reset null
                image: {{Image(service)}}
                pull_policy: never
            """);
    }

    public void ValidateCompose(JsonElement services, IEnumerable<string> selectedServices)
    {
        foreach (var service in selectedServices)
        {
            if (!services.TryGetProperty(service, out var definition) ||
                !definition.TryGetProperty("image", out var image) || image.GetString() != Image(service) ||
                definition.TryGetProperty("build", out _) || HasExecutionOverride(definition, "entrypoint") ||
                HasExecutionOverride(definition, "command"))
                throw new InvalidOperationException("Source mode must preserve the image entrypoint and use its recorded immutable ID without a build: " + service);

            if (definition.TryGetProperty("environment", out var environment) &&
                environment.TryGetProperty("ASPNETCORE_CONTENTROOT", out _))
                throw new InvalidOperationException("Source mode cannot replace image-owned application settings: " + service);

            if (!definition.TryGetProperty("volumes", out var volumes)) continue;

            foreach (var volume in volumes.EnumerateArray())
            {
                var target = volume.GetProperty("target").GetString()!;
                var replacesApplication = target == "/app" || target.StartsWith("/app/", StringComparison.Ordinal);
                var fileName = target.Split('/').Last();

                if (target == "/app/models") replacesApplication = false;

                if (replacesApplication || target == "/scripts" || target.StartsWith("/scripts/", StringComparison.Ordinal) ||
                    target == "/usr/share/nginx/html" || target.StartsWith("/usr/share/nginx/html/", StringComparison.Ordinal) ||
                    fileName.StartsWith("appsettings", StringComparison.OrdinalIgnoreCase) && fileName.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException("Source mode cannot overlay image-owned application files: " + service + " " + target);
            }
        }
    }

    private static bool HasExecutionOverride(JsonElement definition, string property) =>
        definition.TryGetProperty(property, out var value) && value.ValueKind != JsonValueKind.Null;

    private (string Name, string Id) RequiredImage(string service)
    {
        var name = service is "essentia-effnet-worker" or "essentia-maest-worker" ? "essentia" : service;

        if (!_images.TryGetValue(name, out var image))
            throw new InvalidOperationException("The source-image manifest is missing selected service '" + service + "'. Source mode never falls back to cached images.");

        return image;
    }

    private static string RequiredString(JsonElement entry, string name)
    {
        if (!entry.TryGetProperty(name, out var value) || value.ValueKind != JsonValueKind.String ||
            string.IsNullOrWhiteSpace(value.GetString()))
            throw new InvalidOperationException("A source-image build result is missing a nonempty string: " + name);

        return value.GetString()!;
    }
}
