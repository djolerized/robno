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
}
