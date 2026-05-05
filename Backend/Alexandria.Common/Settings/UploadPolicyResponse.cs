namespace Alexandria.Common.Settings;

public class UploadPolicyResponse
{
    public bool SkipClientValidationForTrustedUploads { get; set; }
    public bool SkipUploadOnHashMatch { get; set; }
}