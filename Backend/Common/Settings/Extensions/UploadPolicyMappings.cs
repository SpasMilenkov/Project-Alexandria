using Common.Settings.Values;

namespace Common.Settings.Extensions;

public static class UploadPolicyMappings
{
    public static UploadPolicyResponse ToResponse(UploadPolicyValue v) => new()
    {
        SkipClientValidationForTrustedUploads = v.SkipClientValidationForTrustedUploads,
        SkipUploadOnHashMatch = v.SkipUploadOnHashMatch,
    };

    public static UploadPolicyValue ToValue(UpdateUploadPolicyRequest r) => new()
    {
        SkipClientValidationForTrustedUploads = r.SkipClientValidationForTrustedUploads,
        SkipUploadOnHashMatch = r.SkipUploadOnHashMatch,
    };
}
