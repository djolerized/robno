using Microsoft.EntityFrameworkCore;
using Textil.Planning.Data;
using Textil.Planning.Domain;

namespace Textil.Planning.Tests;

public class ProcurementStageTests
{
    [Theory]
    [InlineData(CycleStatus.Open, 0, ProcurementStage.New)]
    [InlineData(CycleStatus.Open, 1, ProcurementStage.InProgress)]
    [InlineData(CycleStatus.Locked, 0, ProcurementStage.InProgress)]
    [InlineData(CycleStatus.Locked, 2, ProcurementStage.InProgress)]
    [InlineData(CycleStatus.Finalized, 0, ProcurementStage.Closed)]
    [InlineData(CycleStatus.Finalized, 3, ProcurementStage.Closed)]
    public void Stage_follows_status_and_batches(CycleStatus status, int batchCount, ProcurementStage expected) =>
        Assert.Equal(expected, ProcurementStages.Of(status, batchCount));

    [Fact]
    public async Task New_cycle_becomes_in_progress_after_first_upload_and_closed_when_finalized()
    {
        await using var t = await TestServices.CreateAsync();
        var cycles = t.Get<CycleService>();
        var id = await cycles.CreateAsync(await t.IdOf("pop"), "Plan 2027", DateTime.UtcNow.AddDays(10));

        Assert.Equal(ProcurementStage.New, (await cycles.ListAsync()).Single().ProcurementStage);

        await t.Get<ImportService>().ImportAsync(await t.IdOf("mikula"), id, "sku.xlsx", Samples.SkuUpload());
        Assert.Equal(ProcurementStage.InProgress, (await cycles.ListAsync()).Single().ProcurementStage);

        var db = t.Get<AppDbContext>();
        (await db.Cycles.SingleAsync()).Status = CycleStatus.Finalized;
        await db.SaveChangesAsync();
        Assert.Equal(ProcurementStage.Closed, (await cycles.ListAsync()).Single().ProcurementStage);
    }
}
