using Alexandria.Data.Models.Enumerators;
using Microsoft.AspNetCore.Identity;

namespace Alexandria.Data.Models;

public class ApplicationUser : IdentityUser<Guid>, IBase
{
    /// <summary>Default quota for new accounts, in bytes (10 GB). 0 means unlimited.</summary>
    public const long DefaultStorageQuotaBytes = 10_737_418_240L;

    public required string Name { get; set; }
    public DateTime? LockoutStartedAt { get; set; }
    public OnboardingStep OnboardinStep { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public DateTime? DeletedAt { get; set; }
    public Guid? UpdatedBy { get; set; }
    public long StorageQuota { get; set; }
}