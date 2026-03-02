using Microsoft.AspNetCore.Identity;

namespace Models;

public class ApplicationRole : IdentityRole<Guid>, IBase
{
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public DateTime? DeletedAt { get; set; }
    public Guid? UpdatedBy { get; set; }
}