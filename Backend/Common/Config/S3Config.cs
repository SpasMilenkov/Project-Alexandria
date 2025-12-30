namespace Common.Config;

public class S3Config
{
    public required string Endpoint { get; set; }
    public required string SecretKey { get; set; }
    public required string AccessKey { get; set; }
    public required string UploadBucket { get; set; }
    public required string PreviewBucket { get; set; }
    public required string TempBucket { get; set; }
}