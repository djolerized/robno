using Microsoft.AspNetCore.Identity;

namespace Textil.Planning.Data;

public class User : IdentityUser
{
    public string DisplayName { get; set; } = "";

    /// <summary>The user this user submits plans to for approval (team lead for sales, director for team leads).</summary>
    public string? ManagerId { get; set; }

    public User? Manager { get; set; }
}
