namespace Common.Settings.Values
{
    public class UploadPolicyValue
    {
        /// <summary>
        /// When true, the server skips client-side integrity checks on uploads
        /// from trusted sources (e.g. verified clients or internal services).
        /// </summary>
        public bool SkipClientValidationForTrustedUploads { get; set; } = false;

        /// <summary>
        /// When true, if the server already has a file matching the incoming hash
        /// the upload is skipped entirely and the existing object is referenced.
        /// Proof of knowledge — client must supply the correct hash to claim the file.
        /// </summary>
        public bool SkipUploadOnHashMatch { get; set; } = false;
    }
}
