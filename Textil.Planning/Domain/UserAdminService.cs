using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Textil.Planning.Data;
using Textil.Planning.Services;

namespace Textil.Planning.Domain;

public sealed record UserInput(string DisplayName, string Email, IReadOnlyCollection<string> Roles, string? ManagerId);

public sealed record UserSummary(
    string Id, string UserName, string DisplayName, string? Email, IReadOnlySet<string> Roles, string? ManagerId);

/// <summary>User and hierarchy administration. Only the Director may use it.</summary>
public class UserAdminService(AppDbContext db, UserManager<User> userManager, AccessGuard access, IAuditService audit)
{
    public async Task<IReadOnlyList<UserSummary>> ListAsync(string actorId)
    {
        await EnsureDirectorAsync(actorId);
        return await LoadUsersAsync();
    }

    public async Task<UserSummary> GetAsync(string actorId, string userId)
    {
        await EnsureDirectorAsync(actorId);
        return (await LoadUsersAsync()).SingleOrDefault(u => u.Id == userId)
            ?? throw new DomainException("Korisnik ne postoji.");
    }

    public async Task<string> CreateAsync(string actorId, string userName, UserInput input, string password)
    {
        await EnsureDirectorAsync(actorId);
        input = Normalize(input);
        userName = userName.Trim();

        var user = new User
        {
            UserName = userName,
            Email = input.Email,
            EmailConfirmed = true,
            DisplayName = input.DisplayName,
            ManagerId = input.ManagerId,
        };
        Validate(user.Id, input, await LoadUsersAsync());

        await using var tx = await db.Database.BeginTransactionAsync();
        Check(await userManager.CreateAsync(user, password));
        Check(await userManager.AddToRolesAsync(user, input.Roles));
        audit.Add("User", user.Id, "Create", actorId, null,
            new { UserName = userName, input.DisplayName, input.Email, Roles = input.Roles.Order(), input.ManagerId });
        await db.SaveChangesAsync();
        await tx.CommitAsync();
        return user.Id;
    }

    public async Task UpdateAsync(string actorId, string userId, UserInput input)
    {
        await EnsureDirectorAsync(actorId);
        input = Normalize(input);

        var users = await LoadUsersAsync();
        var old = users.SingleOrDefault(u => u.Id == userId) ?? throw new DomainException("Korisnik ne postoji.");
        if (actorId == userId && old.Roles.Contains(Roles.Director) && !input.Roles.Contains(Roles.Director))
            throw new DomainException("Ne možete sebi ukloniti ulogu direktora prodaje.");
        Validate(userId, input, users);

        var user = await userManager.FindByIdAsync(userId) ?? throw new DomainException("Korisnik ne postoji.");
        user.DisplayName = input.DisplayName;
        user.Email = input.Email;
        user.ManagerId = input.ManagerId;

        await using var tx = await db.Database.BeginTransactionAsync();
        Check(await userManager.UpdateAsync(user));
        var removed = old.Roles.Except(input.Roles).ToList();
        var added = input.Roles.Except(old.Roles).ToList();
        if (removed.Count > 0)
            Check(await userManager.RemoveFromRolesAsync(user, removed));
        if (added.Count > 0)
            Check(await userManager.AddToRolesAsync(user, added));
        if (removed.Count > 0 || added.Count > 0)
            Check(await userManager.UpdateSecurityStampAsync(user)); // refreshes role claims of an active session

        audit.Add("User", userId, "Update", actorId,
            new { old.DisplayName, old.Email, Roles = old.Roles.Order(), old.ManagerId },
            new { input.DisplayName, input.Email, Roles = input.Roles.Order(), input.ManagerId });
        await db.SaveChangesAsync();
        await tx.CommitAsync();
    }

    public async Task ResetPasswordAsync(string actorId, string userId, string newPassword)
    {
        await EnsureDirectorAsync(actorId);
        var user = await userManager.FindByIdAsync(userId) ?? throw new DomainException("Korisnik ne postoji.");

        await using var tx = await db.Database.BeginTransactionAsync();
        Check(await userManager.RemovePasswordAsync(user));
        Check(await userManager.AddPasswordAsync(user, newPassword));
        audit.Add("User", userId, "ResetPassword", actorId, null, null);
        await db.SaveChangesAsync();
        await tx.CommitAsync();
    }

    private Task EnsureDirectorAsync(string actorId) => access.EnsureAnyRoleAsync(actorId, Roles.Director);

    private async Task<IReadOnlyList<UserSummary>> LoadUsersAsync()
    {
        var users = await db.Users.AsNoTracking().OrderBy(u => u.DisplayName).ToListAsync();
        var userRoles = await (
            from ur in db.UserRoles
            join r in db.Roles on ur.RoleId equals r.Id
            select new { ur.UserId, r.Name }).ToListAsync();
        var rolesByUser = userRoles.ToLookup(x => x.UserId, x => x.Name!);

        return users
            .Select(u => new UserSummary(u.Id, u.UserName ?? "", u.DisplayName, u.Email,
                rolesByUser[u.Id].ToHashSet(), u.ManagerId))
            .ToList();
    }

    private static void Validate(string userId, UserInput input, IReadOnlyList<UserSummary> users)
    {
        var errors = new List<string>();
        if (input.DisplayName.Length == 0)
            errors.Add("Ime je obavezno.");
        var candidate = new UserNode(userId, input.DisplayName, input.Roles.ToHashSet(), input.ManagerId);
        errors.AddRange(HierarchyRules.Validate(candidate, users.Select(u => new UserNode(u.Id, u.DisplayName, u.Roles, u.ManagerId))));
        if (errors.Count > 0)
            throw new DomainException(errors);
    }

    private static UserInput Normalize(UserInput input) => new(
        input.DisplayName.Trim(),
        input.Email.Trim(),
        input.Roles.Distinct().ToList(),
        string.IsNullOrWhiteSpace(input.ManagerId) ? null : input.ManagerId);

    private static void Check(IdentityResult result)
    {
        if (!result.Succeeded)
            throw new DomainException(result.Errors.Select(e => e.Description).ToList());
    }
}
