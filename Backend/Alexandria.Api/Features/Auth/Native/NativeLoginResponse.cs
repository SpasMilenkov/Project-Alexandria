using Alexandria.Api.Features.Auth.Login;

namespace Alexandria.Api.Features.Auth.Native;

public sealed class NativeLoginResponse : LoginResponse
{
    public required string AccessToken { get; set; }
    public required string RefreshToken { get; set; }
}