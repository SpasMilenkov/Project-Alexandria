using DTO;
using DTO.Files;

namespace API.Features.Auth.Login;

public class LoginResponse
{
    public bool Success { get; set; }
    public UserDto? User { get; set; }
    public string CsrfToken { get; set; } = string.Empty;
}
