using System.Text.Json.Serialization;

namespace Builder.Models;

[JsonSerializable(typeof(InstallationConfig))]
[JsonSerializable(typeof(Environment))]
[JsonSerializable(typeof(S3Provider))]
[JsonSerializable(typeof(Dictionary<string, int>))]
[JsonSerializable(typeof(Dictionary<string, string>))]
[JsonSourceGenerationOptions(WriteIndented = true)]
public partial class AlexandriaJsonContext : JsonSerializerContext
{
}