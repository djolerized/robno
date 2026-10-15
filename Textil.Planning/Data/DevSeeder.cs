using ClosedXML.Excel;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
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

    public static readonly string[] SeedSkus = ["TX-10231", "TX-10232", "TX-10555", "TX-11002", "TX-11310"];

    /// <summary>
    /// Creates the open cycle "Plan 2027" with batch #1 through the real services, as pop and mikula.
    /// Skipped when any cycle exists.
    /// </summary>
    public static async Task SeedCycleAsync(
        AppDbContext db, UserManager<User> userManager, CycleService cycles, ImportService import, TimeProvider clock)
    {
        if (await db.Cycles.AnyAsync())
            return;

        var pop = await userManager.FindByNameAsync("pop") ?? throw new InvalidOperationException("Seed users first.");
        var mikula = await userManager.FindByNameAsync("mikula") ?? throw new InvalidOperationException("Seed users first.");

        var deadline = clock.GetUtcNow().UtcDateTime.Date.AddDays(30).AddHours(15);
        var cycleId = await cycles.CreateAsync(pop.Id, "Plan 2027", deadline);
        await import.ImportAsync(mikula.Id, cycleId, "plan-2027_batch1.xlsx",
            BuildSkuFile(["2027-01", "2027-02", "2027-03", "2027-04", "2027-05", "2027-06"], SeedSkus));
    }

    public static byte[] BuildSkuFile(IReadOnlyList<string> months, IReadOnlyList<string> codes)
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.AddWorksheet("Plan");
        sheet.Cell(1, 1).Value = "SKU";
        for (var i = 0; i < months.Count; i++)
            sheet.Cell(1, i + 2).Value = months[i];
        for (var i = 0; i < codes.Count; i++)
            sheet.Cell(i + 2, 1).Value = codes[i];
        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
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
