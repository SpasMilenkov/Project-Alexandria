namespace API.Config;

public class MinioConfig
{
    public string? Endpoint { get; set; }
    public string? SecretKey { get; set; }
    public string? AccessKey { get; set; }
    public string? UploadBucket { get; set; }
}