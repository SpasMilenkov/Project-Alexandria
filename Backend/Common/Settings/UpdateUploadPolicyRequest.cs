namespace Common.Settings;

public class UpdateUploadPolicyRequest
{
    public bool SkipClientValidationForTrustedUploads { get; set; }
    public bool SkipUploadOnHashMatch { get; set; }
}
