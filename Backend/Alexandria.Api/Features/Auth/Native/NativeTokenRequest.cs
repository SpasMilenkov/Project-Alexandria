namespace Alexandria.Api.Features.Auth.Native;

public sealed class NativeTokenRequest
{
    public string RefreshToken { get; set; } = string.Empty;
}