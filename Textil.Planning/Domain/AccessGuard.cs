using Microsoft.EntityFrameworkCore;
using Textil.Planning.Data;

namespace Textil.Planning.Domain;

/// <summary>Role lookups against the database (not the cookie), so a revoked role takes effect immediately.</summary>
public class AccessGuard(AppDbContext db)
{
    public Task<bool> HasAnyRoleAsync(string userId, params string[] roles) => (
        from ur in db.UserRoles
        join r in db.Roles on ur.RoleId equals r.Id
        where ur.UserId == userId && roles.Contains(r.Name!)
        select ur).AnyAsync();

    public async Task EnsureAnyRoleAsync(string userId, params string[] roles)
    {
        if (!await HasAnyRoleAsync(userId, roles))
            throw new PermissionDeniedException();
    }

    public Task<List<string>> UserIdsInRoleAsync(string role) => (
        from ur in db.UserRoles
        join r in db.Roles on ur.RoleId equals r.Id
        where r.Name == role
        select ur.UserId).ToListAsync();
}
