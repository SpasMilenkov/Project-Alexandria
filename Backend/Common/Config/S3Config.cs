namespace Common.Config;

public class S3Config
{
    public string? Endpoint { get; set; }
    public string? SecretKey { get; set; }
    public string? AccessKey { get; set; }
    public string? UploadBucket { get; set; }
    public string? PreviewBucket { get; set; }
}