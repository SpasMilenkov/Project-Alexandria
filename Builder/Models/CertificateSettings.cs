namespace Builder.Models;

public sealed class CertificateSettings
{
    public const string GeneratedMode = "generated";
    public const string ImportedMode = "imported";

    public string Mode { get; set; } = GeneratedMode;
    public string ImportCertificatePath { get; set; } = string.Empty;
    public string ImportPrivateKeyPath { get; set; } = string.Empty;
}
