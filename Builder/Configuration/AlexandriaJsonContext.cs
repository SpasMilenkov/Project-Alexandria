using System.Text.Json.Serialization;
using Builder.Catalog;
using Builder.Certificates;
using Builder.Credentials;
using Builder.Network;
using Builder.Preferences;
using Builder.SystemCheck;

namespace Builder.Configuration;

[JsonSerializable(typeof(InstallationConfig))]
[JsonSerializable(typeof(GenerationCheckpoint))]
[JsonSerializable(typeof(PublicEndpoint))]
[JsonSerializable(typeof(CertificateSettings))]
[JsonSerializable(typeof(FeatureSelection))]
[JsonSerializable(typeof(SystemResources))]
[JsonSerializable(typeof(PortMapping))]
[JsonSerializable(typeof(GarageCredentialSet))]
[JsonSerializable(typeof(UiPreferences))]
[JsonSerializable(typeof(Dictionary<string, int>))]
[JsonSerializable(typeof(Dictionary<string, string>))]
[JsonSerializable(typeof(List<PortMapping>))]
[JsonSourceGenerationOptions(WriteIndented = true)]
public partial class AlexandriaJsonContext : JsonSerializerContext
{
}
