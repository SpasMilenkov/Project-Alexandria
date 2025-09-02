using System.ComponentModel.DataAnnotations;

namespace API.Features.GenerateSignedUrlEndpoint;

public class GenerateSignedUrlRequest
{
    [Required] public string Name { get; set; } = null!;

    public string? Path { get; set; }

    // e.g. TimeSpan.FromMinutes(15)
    public TimeSpan Expiry { get; set; } = TimeSpan.FromMinutes(5);
}