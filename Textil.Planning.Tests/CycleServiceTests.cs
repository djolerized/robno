using Microsoft.EntityFrameworkCore;
using Textil.Planning.Data;
using Textil.Planning.Domain;

namespace Textil.Planning.Tests;

public class CycleServiceTests
{
    [Fact]
    public async Task Director_creates_open_cycle_without_months()
    {
        await using var t = await TestServices.CreateAsync();
        var pop = await t.IdOf("pop");
        var deadline = DateTime.UtcNow.AddDays(10);

        var id = await t.Get<CycleService>().CreateAsync(pop, "  Plan 2027 ", deadline);

        var cycle = await t.Get<AppDbContext>().Cycles.Include(c => c.Months).SingleAsync(c => c.Id == id);
        Assert.Equal("Plan 2027", cycle.Name);
        Assert.Equal(CycleStatus.Open, cycle.Status);
        Assert.Equal(deadline, cycle.Deadline);
        Assert.Equal(pop, cycle.CreatedById);
        Assert.Empty(cycle.Months);
        Assert.True(await t.Get<AppDbContext>().AuditLogs.AnyAsync(a => a.Entity == "Cycle" && a.Action == "Create"));
    }

    [Theory]
    [InlineData("mikula")]
    [InlineData("milan")]
    [InlineData("tijana")]
    public async Task Only_director_creates_cycles(string actor)
    {
        await using var t = await TestServices.CreateAsync();
        var actorId = await t.IdOf(actor);

        await Assert.ThrowsAsync<PermissionDeniedException>(() =>
            t.Get<CycleService>().CreateAsync(actorId, "Plan 2027", DateTime.UtcNow.AddDays(10)));
    }

    [Fact]
    public async Task Deadline_must_be_in_future_and_name_unique()
    {
        await using var t = await TestServices.CreateAsync();
        var pop = await t.IdOf("pop");
        var service = t.Get<CycleService>();

        await Assert.ThrowsAsync<DomainException>(() => service.CreateAsync(pop, "Plan 2027", DateTime.UtcNow.AddMinutes(-1)));
        await Assert.ThrowsAsync<DomainException>(() => service.CreateAsync(pop, " ", DateTime.UtcNow.AddDays(1)));
        await service.CreateAsync(pop, "Plan 2027", DateTime.UtcNow.AddDays(1));
        await Assert.ThrowsAsync<DomainException>(() => service.CreateAsync(pop, "Plan 2027", DateTime.UtcNow.AddDays(1)));
    }

    [Fact]
    public async Task Deadline_changes_only_while_open()
    {
        await using var t = await TestServices.CreateAsync();
        var pop = await t.IdOf("pop");
        var service = t.Get<CycleService>();
        var id = await service.CreateAsync(pop, "Plan 2027", DateTime.UtcNow.AddDays(1));

        var later = DateTime.UtcNow.AddDays(20);
        await service.ChangeDeadlineAsync(pop, id, later);
        var db = t.Get<AppDbContext>();
        var cycle = await db.Cycles.SingleAsync(c => c.Id == id);
        Assert.Equal(later, cycle.Deadline);

        cycle.Status = CycleStatus.Locked;
        await db.SaveChangesAsync();
        await Assert.ThrowsAsync<DomainException>(() => service.ChangeDeadlineAsync(pop, id, DateTime.UtcNow.AddDays(30)));
    }

    [Fact]
    public async Task Batch_history_is_hidden_from_sales()
    {
        await using var t = await TestServices.CreateAsync();
        var id = await t.Get<CycleService>().CreateAsync(await t.IdOf("pop"), "Plan 2027", DateTime.UtcNow.AddDays(1));

        var tijana = await t.IdOf("tijana");

        await Assert.ThrowsAsync<PermissionDeniedException>(() => t.Get<CycleService>().ListBatchesAsync(tijana, id));
        Assert.Empty(await t.Get<CycleService>().ListBatchesAsync(await t.IdOf("milan"), id));
    }
}
