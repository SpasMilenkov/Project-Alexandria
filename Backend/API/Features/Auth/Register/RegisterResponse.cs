namespace API.Features.Auth.Register;

public class RegisterResponse
{
    public bool Success { get; set; }
    public Guid? UserId { get; set; }
    public string? Message { get; set; }
    public Dictionary<string, List<string>>? Errors { get; set; }
}
