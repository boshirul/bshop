using Microsoft.AspNetCore.Identity;

namespace RetailShop.Infrastructure.Identity;

public sealed class ApplicationUser : IdentityUser<Guid>
{
    public ApplicationUser()
    {
        Id = Guid.CreateVersion7();
        SecurityStamp = Guid.NewGuid().ToString();
    }

    public string FullName { get; set; } = string.Empty;

    // A storage key under the application-managed persistent media root, never a client URL.
    public string? AvatarStorageKey { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTimeOffset CreatedOn { get; set; } = DateTimeOffset.UtcNow;

    public Guid? CreatedBy { get; set; }

    public DateTimeOffset? LastModifiedOn { get; set; }

    public Guid? LastModifiedBy { get; set; }

    public ICollection<RefreshToken> RefreshTokens { get; set; } = [];
}
