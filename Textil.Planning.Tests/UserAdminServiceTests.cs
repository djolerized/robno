using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Textil.Planning.Data;
using Textil.Planning.Domain;

namespace Textil.Planning.Tests;

public class UserAdminServiceTests
{
    [Fact]
    public async Task Director_creates_sales_user_under_team_lead()
    {
        await using var t = await TestServices.CreateAsync();
        var pop = await t.IdOf("pop");
        var milan = await t.IdOf("milan");

        var id = await t.Get<UserAdminService>().CreateAsync(pop, "jelena",
            new UserInput(" Jelena ", "jelena@textil.local", [Roles.Sales], milan), "Lozinka123!");

        var user = await t.Get<UserManager<User>>().FindByIdAsync(id);
        Assert.Equal("Jelena", user!.DisplayName);
        Assert.Equal(milan, user.ManagerId);
        Assert.True(await t.Get<UserManager<User>>().IsInRoleAsync(user, Roles.Sales));
        Assert.True(await t.Get<UserManager<User>>().CheckPasswordAsync(user, "Lozinka123!"));
    }

    [Theory]
    [InlineData("milan")]
    [InlineData("tijana")]
    [InlineData("mikula")]
    public async Task Only_director_may_administer_users(string actor)
    {
        await using var t = await TestServices.CreateAsync();
        var actorId = await t.IdOf(actor);
        var service = t.Get<UserAdminService>();

        await Assert.ThrowsAsync<PermissionDeniedException>(() => service.ListAsync(actorId));
        await Assert.ThrowsAsync<PermissionDeniedException>(() => service.CreateAsync(actorId, "x",
            new UserInput("X", "x@textil.local", [Roles.Procurement], null), "Lozinka123!"));
        await Assert.ThrowsAsync<PermissionDeniedException>(() =>
            service.ResetPasswordAsync(actorId, actorId, "Lozinka123!"));
    }

    [Fact]
    public async Task Invalid_hierarchy_is_rejected_and_nothing_is_saved()
    {
        await using var t = await TestServices.CreateAsync();
        var pop = await t.IdOf("pop");

        await Assert.ThrowsAsync<DomainException>(() => t.Get<UserAdminService>().CreateAsync(pop, "jelena",
            new UserInput("Jelena", "jelena@textil.local", [Roles.Sales], pop), "Lozinka123!"));

        Assert.Null(await t.Get<UserManager<User>>().FindByNameAsync("jelena"));
    }

    [Fact]
    public async Task Weak_password_is_rejected_with_serbian_message_and_rolled_back()
    {
        await using var t = await TestServices.CreateAsync();
        var pop = await t.IdOf("pop");

        var ex = await Assert.ThrowsAsync<DomainException>(() => t.Get<UserAdminService>().CreateAsync(pop, "jelena",
            new UserInput("Jelena", "jelena@textil.local", [Roles.Procurement], null), "kratka"));

        Assert.Contains(ex.Errors, e => e.Contains("Lozinka"));
        Assert.Null(await t.Get<UserManager<User>>().FindByNameAsync("jelena"));
    }

    [Fact]
    public async Task Duplicate_email_is_rejected()
    {
        await using var t = await TestServices.CreateAsync();
        var pop = await t.IdOf("pop");

        await Assert.ThrowsAsync<DomainException>(() => t.Get<UserAdminService>().CreateAsync(pop, "jelena",
            new UserInput("Jelena", "tijana@textil.local", [Roles.Procurement], null), "Lozinka123!"));
    }

    [Fact]
    public async Task Update_moves_user_and_changes_roles()
    {
        await using var t = await TestServices.CreateAsync();
        var pop = await t.IdOf("pop");
        var tijana = await t.IdOf("tijana");

        // Tijana becomes a team lead reporting to the director.
        await t.Get<UserAdminService>().UpdateAsync(pop, tijana,
            new UserInput("Tijana", "tijana@textil.local", [Roles.Sales, Roles.TeamLead], pop));

        var summary = await t.Get<UserAdminService>().GetAsync(pop, tijana);
        Assert.Equal(pop, summary.ManagerId);
        Assert.True(summary.Roles.SetEquals([Roles.Sales, Roles.TeamLead]));
    }

    [Fact]
    public async Task Team_lead_role_cannot_be_removed_while_team_reports_to_them()
    {
        await using var t = await TestServices.CreateAsync();
        var pop = await t.IdOf("pop");
        var milan = await t.IdOf("milan");

        var ex = await Assert.ThrowsAsync<DomainException>(() => t.Get<UserAdminService>().UpdateAsync(pop, milan,
            new UserInput("Milan", "milan@textil.local", [Roles.Sales], pop)));

        Assert.Contains(ex.Errors, e => e.Contains("Tijana"));
    }

    [Fact]
    public async Task Director_cannot_remove_own_director_role()
    {
        await using var t = await TestServices.CreateAsync();
        var pop = await t.IdOf("pop");

        await Assert.ThrowsAsync<DomainException>(() => t.Get<UserAdminService>().UpdateAsync(pop, pop,
            new UserInput("Pop", "pop@textil.local", [Roles.Procurement], null)));
    }

    [Fact]
    public async Task Changes_are_audited()
    {
        await using var t = await TestServices.CreateAsync();
        var pop = await t.IdOf("pop");
        var tijana = await t.IdOf("tijana");
        var service = t.Get<UserAdminService>();

        await service.UpdateAsync(pop, tijana, new UserInput("Tijana P.", "tijana@textil.local", [Roles.Sales], await t.IdOf("milan")));
        await service.ResetPasswordAsync(pop, tijana, "NovaLozinka1!");

        var log = await t.Get<AppDbContext>().AuditLogs.Where(a => a.EntityId == tijana).OrderBy(a => a.Id).ToListAsync();
        Assert.Equal(["Update", "ResetPassword"], log.Select(a => a.Action));
        Assert.All(log, a => Assert.Equal(pop, a.UserId));
        Assert.Contains("\"Tijana\"", log[0].OldValue);
        Assert.Contains("\"Tijana P.\"", log[0].NewValue);
    }

    [Fact]
    public async Task Reset_password_replaces_old_password()
    {
        await using var t = await TestServices.CreateAsync();
        var pop = await t.IdOf("pop");
        var tijana = await t.IdOf("tijana");

        await t.Get<UserAdminService>().ResetPasswordAsync(pop, tijana, "NovaLozinka1!");

        var user = await t.Get<UserManager<User>>().FindByIdAsync(tijana);
        Assert.False(await t.Get<UserManager<User>>().CheckPasswordAsync(user!, TestServices.Password));
        Assert.True(await t.Get<UserManager<User>>().CheckPasswordAsync(user!, "NovaLozinka1!"));
    }
}
