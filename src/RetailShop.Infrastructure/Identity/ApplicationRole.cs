using Microsoft.AspNetCore.Identity;

namespace RetailShop.Infrastructure.Identity;

public sealed class ApplicationRole : IdentityRole<Guid>
{
    public ApplicationRole()
    {
        Id = Guid.CreateVersion7();
    }

    public ApplicationRole(string name) : this()
    {
        Name = name;
    }
}
