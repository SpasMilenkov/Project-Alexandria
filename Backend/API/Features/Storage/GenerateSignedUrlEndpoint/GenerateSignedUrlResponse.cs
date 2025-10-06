namespace API.Features.Storage.GenerateSignedUrlEndpoint;

public class GenerateSignedUrlResponse
{
    public required string Url { get; set; }
    public DateTime ExpiresAt { get; set; }
}