using Microsoft.AspNetCore.Identity;
using Textil.Planning.Domain;

namespace Textil.Planning.Data;

public static class DevSeeder
{
    public static async Task EnsureRolesAsync(RoleManager<IdentityRole> roleManager)
    {
        foreach (var role in Roles.All)
        {
            if (!await roleManager.RoleExistsAsync(role))
                Check(await roleManager.CreateAsync(new IdentityRole(role)));
        }
    }

    /// <summary>Creates the development test accounts. Idempotent: existing accounts are left untouched.</summary>
    public static async Task SeedUsersAsync(UserManager<User> userManager, string password)
    {
        // Managers first, so ManagerId can be resolved.
        var pop = await EnsureUserAsync(userManager, password, "pop", "Pop", null, Roles.Director);
        await EnsureUserAsync(userManager, password, "mikula", "Mikula", null, Roles.Procurement);
        var milan = await EnsureUserAsync(userManager, password, "milan", "Milan", pop.Id, Roles.Sales, Roles.TeamLead);
        await EnsureUserAsync(userManager, password, "tijana", "Tijana", milan.Id, Roles.Sales);
        await EnsureUserAsync(userManager, password, "marko", "Marko", milan.Id, Roles.Sales);
    }

    private static async Task<User> EnsureUserAsync(
        UserManager<User> userManager, string password, string userName, string displayName, string? managerId, params string[] roles)
    {
        var user = await userManager.FindByNameAsync(userName);
        if (user is not null)
            return user;

        user = new User
        {
            UserName = userName,
            Email = $"{userName}@textil.local",
            EmailConfirmed = true,
            DisplayName = displayName,
            ManagerId = managerId,
        };
        Check(await userManager.CreateAsync(user, password));
        Check(await userManager.AddToRolesAsync(user, roles));
        return user;
    }

    private static void Check(IdentityResult result)
    {
        if (!result.Succeeded)
            throw new InvalidOperationException("Seed failed: " + string.Join("; ", result.Errors.Select(e => e.Description)));
    }
}
