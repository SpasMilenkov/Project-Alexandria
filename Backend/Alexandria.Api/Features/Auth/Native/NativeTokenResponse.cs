namespace Alexandria.Api.Features.Auth.Native;

public sealed class NativeTokenResponse
{
    public required string AccessToken { get; set; }
    public required string RefreshToken { get; set; }
}