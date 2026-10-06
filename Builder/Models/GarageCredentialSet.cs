namespace Builder.Models;

// Exact contract of init-output/garage-credentials.env written by
// scripts/garage-init/init.sh (locked decision D6). The file is deleted
// immediately after a successful parse so no plaintext duplicate remains.
public sealed record GarageCredentialSet(
    string AccessKey,
    string AccessSecret,
    string PreviewKeyId,
    string PreviewSecret,
    string StreamingKeyId,
    string StreamingSecret)
{
    public bool IsComplete =>
        !string.IsNullOrWhiteSpace(AccessKey) &&
        !string.IsNullOrWhiteSpace(AccessSecret) &&
        !string.IsNullOrWhiteSpace(PreviewKeyId) &&
        !string.IsNullOrWhiteSpace(PreviewSecret) &&
        !string.IsNullOrWhiteSpace(StreamingKeyId) &&
        !string.IsNullOrWhiteSpace(StreamingSecret);
}
