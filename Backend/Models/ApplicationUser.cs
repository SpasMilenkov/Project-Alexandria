using Microsoft.AspNetCore.Identity;

namespace Models;

public class ApplicationUser: IdentityUser<Guid>, IBase
{
    public required string Name { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public DateTime? DeletedAt { get; set; }
    public Guid? UpdatedBy { get; set; }
}