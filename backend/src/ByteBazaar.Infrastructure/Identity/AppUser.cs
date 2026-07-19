using Microsoft.AspNetCore.Identity;

namespace ByteBazaar.Infrastructure.Identity;

public class AppUser : IdentityUser<Guid>
{
    public string FullName { get; set; } = string.Empty;
    public string? Phone { get; set; }
}
