using System.Text.Json.Serialization;

namespace Builder.Models;

[JsonSerializable(typeof(InstallationConfig))]
[JsonSerializable(typeof(FeatureSelection))]
[JsonSerializable(typeof(SystemResources))]
[JsonSerializable(typeof(PortMapping))]
[JsonSerializable(typeof(Environment))]
[JsonSerializable(typeof(S3Provider))]
[JsonSerializable(typeof(Dictionary<string, int>))]
[JsonSerializable(typeof(Dictionary<string, string>))]
[JsonSerializable(typeof(List<PortMapping>))]
[JsonSourceGenerationOptions(WriteIndented = true)]
public partial class AlexandriaJsonContext : JsonSerializerContext
{
}
