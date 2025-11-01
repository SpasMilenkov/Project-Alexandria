namespace API.Features.Auth.RefreshToken;

public class RefreshResponse
{
    public bool Success { get; set; }
    public string CsrfToken { get; set; } = string.Empty;
}