using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Textil.Planning.Data;
using Textil.Planning.Domain;

namespace Textil.Planning.Tests;

public class DevSeederTests
{
    [Fact]
    public async Task Seeds_test_accounts_with_hierarchy_and_is_idempotent()
    {
        await using var t = await TestServices.CreateAsync();
        var userManager = t.Get<UserManager<User>>();
        await DevSeeder.SeedUsersAsync(userManager, TestServices.Password);

        Assert.Equal(5, await t.Get<AppDbContext>().Users.CountAsync());

        var pop = await t.IdOf("pop");
        var milan = await t.IdOf("milan");
        var expected = new (string UserName, string? ManagerId, string[] Roles)[]
        {
            ("mikula", null, [Roles.Procurement]),
            ("tijana", milan, [Roles.Sales]),
            ("marko", milan, [Roles.Sales]),
            ("milan", pop, [Roles.Sales, Roles.TeamLead]),
            ("pop", null, [Roles.Director]),
        };
        foreach (var (userName, managerId, roles) in expected)
        {
            var user = (await userManager.FindByNameAsync(userName))!;
            Assert.Equal(managerId, user.ManagerId);
            Assert.True((await userManager.GetRolesAsync(user)).ToHashSet().SetEquals(roles), userName);
            Assert.True(await userManager.CheckPasswordAsync(user, TestServices.Password), userName);
        }
    }

    [Fact]
    public async Task Seeds_open_cycle_with_first_batch_once()
    {
        await using var t = await TestServices.CreateAsync();
        for (var i = 0; i < 2; i++)
        {
            await DevSeeder.SeedCycleAsync(t.Get<AppDbContext>(), t.Get<UserManager<User>>(),
                t.Get<CycleService>(), t.Get<ImportService>(), TimeProvider.System);
        }

        var db = t.Get<AppDbContext>();
        var cycle = await db.Cycles.Include(c => c.Months).SingleAsync();
        Assert.Equal("Plan 2027", cycle.Name);
        Assert.Equal(CycleStatus.Open, cycle.Status);
        Assert.Equal(6, cycle.Months.Count);
        Assert.Equal(DevSeeder.SeedSkus, await db.CycleSkus.OrderBy(s => s.Order).Select(s => s.Code).ToListAsync());
        Assert.Equal(3, await db.Approvals.CountAsync());
    }
}
