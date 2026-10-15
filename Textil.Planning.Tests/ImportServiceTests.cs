using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using Textil.Planning.Data;
using Textil.Planning.Domain;

namespace Textil.Planning.Tests;

public class ImportServiceTests
{
    private static async Task<(TestServices T, int CycleId, string Mikula)> SetupAsync()
    {
        var t = await TestServices.CreateAsync();
        var cycleId = await t.Get<CycleService>().CreateAsync(await t.IdOf("pop"), "Plan 2027", DateTime.UtcNow.AddDays(30));
        return (t, cycleId, await t.IdOf("mikula"));
    }

    private static Task<ImportResult> Import(TestServices t, string actor, int cycleId, byte[] file) =>
        t.Get<ImportService>().ImportAsync(actor, cycleId, "plan.xlsx", file);

    [Fact]
    public async Task Valid_file_creates_batch_with_skus_in_file_order()
    {
        var (t, cycleId, mikula) = await SetupAsync();
        await using var _ = t;
        var sample = Samples.SkuUpload();

        var result = await Import(t, mikula, cycleId, sample);

        Assert.Equal(1, result.BatchNumber);
        Assert.Equal(8, result.ImportedCount);
        Assert.Empty(result.DuplicatesInFile);
        Assert.Empty(result.DuplicatesInCycle);

        var db = t.Get<AppDbContext>();
        var skus = await db.CycleSkus.Where(s => s.BatchId == result.BatchId).OrderBy(s => s.Order).ToListAsync();
        Assert.Equal(Samples.Codes, skus.Select(s => s.Code));
        Assert.Equal(Enumerable.Range(1, 8), skus.Select(s => s.Order));

        var months = await db.CycleMonths.Where(m => m.CycleId == cycleId).OrderBy(m => m.Order).Select(m => m.Month).ToListAsync();
        Assert.Equal(Samples.Months, months);

        var batch = await db.Batches.SingleAsync(b => b.Id == result.BatchId);
        Assert.Equal(sample, batch.OriginalFile);
        Assert.Equal("plan.xlsx", batch.OriginalFileName);
        Assert.Equal(mikula, batch.UploadedById);
    }

    [Fact]
    public async Task Batch_creates_draft_approval_for_every_sales_user()
    {
        var (t, cycleId, mikula) = await SetupAsync();
        await using var _ = t;

        var result = await Import(t, mikula, cycleId, Samples.SkuUpload());

        var approvals = await t.Get<AppDbContext>().Approvals.Where(a => a.BatchId == result.BatchId).ToListAsync();
        var expected = new[] { await t.IdOf("tijana"), await t.IdOf("marko"), await t.IdOf("milan") };
        Assert.Equal(expected.Order(), approvals.Select(a => a.UserId).Order());
        Assert.All(approvals, a => Assert.Equal(ApprovalStatus.Draft, a.Status));
    }

    [Fact]
    public async Task Duplicate_within_file_is_skipped_with_warning()
    {
        var (t, cycleId, mikula) = await SetupAsync();
        await using var _ = t;
        var file = Samples.SkuUpload(sheet =>
        {
            sheet.Cell(10, 1).Value = "TX-10232";
            sheet.Cell(11, 1).Value = "tx-10555"; // same code, different case
            sheet.Cell(12, 1).Value = "TX-20000";
        });

        var result = await Import(t, mikula, cycleId, file);

        Assert.Equal(9, result.ImportedCount);
        Assert.Equal(["TX-10232", "tx-10555"], result.DuplicatesInFile);
        Assert.Empty(result.DuplicatesInCycle);
        Assert.Equal(1, await t.Get<AppDbContext>().CycleSkus.CountAsync(s => s.Code == "TX-10232"));
    }

    [Fact]
    public async Task Duplicate_already_in_cycle_is_skipped_and_new_codes_continue_order()
    {
        var (t, cycleId, mikula) = await SetupAsync();
        await using var _ = t;
        await Import(t, mikula, cycleId, Samples.SkuUpload());

        var second = Samples.SkuUpload(sheet =>
        {
            sheet.Rows(4, 9).Delete(); // keeps TX-10231, TX-10232
            sheet.Cell(4, 1).Value = "TX-30001";
            sheet.Cell(5, 1).Value = "TX-30002";
        });
        var result = await Import(t, mikula, cycleId, second);

        Assert.Equal(2, result.BatchNumber);
        Assert.Equal(2, result.ImportedCount);
        Assert.Equal(["TX-10231", "TX-10232"], result.DuplicatesInCycle);
        var newSkus = await t.Get<AppDbContext>().CycleSkus.Where(s => s.BatchId == result.BatchId).OrderBy(s => s.Order).ToListAsync();
        Assert.Equal(["TX-30001", "TX-30002"], newSkus.Select(s => s.Code));
        Assert.Equal([9, 10], newSkus.Select(s => s.Order));
    }

    [Fact]
    public async Task New_batch_does_not_touch_existing_approvals()
    {
        var (t, cycleId, mikula) = await SetupAsync();
        await using var _ = t;
        var first = await Import(t, mikula, cycleId, Samples.SkuUpload());
        var db = t.Get<AppDbContext>();
        var approval = await db.Approvals.FirstAsync(a => a.BatchId == first.BatchId);
        approval.Status = ApprovalStatus.Submitted;
        await db.SaveChangesAsync();

        var second = await Import(t, mikula, cycleId, Samples.SkuUpload(sheet => sheet.Cell(10, 1).Value = "TX-30001"));

        Assert.Equal(ApprovalStatus.Submitted, (await db.Approvals.SingleAsync(a => a.Id == approval.Id)).Status);
        Assert.Equal(3, await db.Approvals.CountAsync(a => a.BatchId == second.BatchId && a.Status == ApprovalStatus.Draft));
    }

    [Fact]
    public async Task File_with_only_duplicates_is_rejected()
    {
        var (t, cycleId, mikula) = await SetupAsync();
        await using var _ = t;
        await Import(t, mikula, cycleId, Samples.SkuUpload());

        await Assert.ThrowsAsync<DomainException>(() => Import(t, mikula, cycleId, Samples.SkuUpload()));
        Assert.Equal(1, await t.Get<AppDbContext>().Batches.CountAsync());
    }

    [Fact]
    public async Task Different_months_than_cycle_are_rejected()
    {
        var (t, cycleId, mikula) = await SetupAsync();
        await using var _ = t;
        await Import(t, mikula, cycleId, Samples.SkuUpload());

        var shifted = Samples.SkuUpload(sheet =>
        {
            sheet.Column(2).Delete(); // drop 2027-01
            sheet.Cell(1, 7).Value = "2027-07";
            sheet.Cell(10, 1).Value = "TX-30001";
        });
        var ex = await Assert.ThrowsAsync<DomainException>(() => Import(t, mikula, cycleId, shifted));

        Assert.Contains(ex.Errors, e => e.Contains("Nedostaju: 2027-01"));
        Assert.Contains(ex.Errors, e => e.Contains("Višak: 2027-07"));
        Assert.Equal(1, await t.Get<AppDbContext>().Batches.CountAsync());
    }

    [Fact]
    public async Task Fewer_months_than_cycle_are_rejected()
    {
        var (t, cycleId, mikula) = await SetupAsync();
        await using var _ = t;
        await Import(t, mikula, cycleId, Samples.SkuUpload());

        var shorter = Samples.SkuUpload(sheet =>
        {
            sheet.Column(7).Delete();
            sheet.Cell(10, 1).Value = "TX-30001";
        });
        var ex = await Assert.ThrowsAsync<DomainException>(() => Import(t, mikula, cycleId, shorter));
        Assert.Contains(ex.Errors, e => e.Contains("Nedostaju: 2027-06"));
    }

    [Fact]
    public async Task Header_only_file_is_rejected()
    {
        var (t, cycleId, mikula) = await SetupAsync();
        await using var _ = t;
        var headerOnly = Samples.SkuUpload(sheet => sheet.Rows(2, 9).Delete());

        var ex = await Assert.ThrowsAsync<DomainException>(() => Import(t, mikula, cycleId, headerOnly));
        Assert.Contains(ex.Errors, e => e.Contains("nijednu šifru"));
        Assert.Empty(await t.Get<AppDbContext>().CycleMonths.ToListAsync());
    }

    [Fact]
    public async Task Empty_sheet_and_empty_upload_are_rejected()
    {
        var (t, cycleId, mikula) = await SetupAsync();
        await using var _ = t;
        var emptySheet = Samples.SkuUpload(sheet => sheet.Clear());

        var ex = await Assert.ThrowsAsync<DomainException>(() => Import(t, mikula, cycleId, emptySheet));
        Assert.Contains("Fajl je prazan.", ex.Errors);
        await Assert.ThrowsAsync<DomainException>(() => Import(t, mikula, cycleId, []));
    }

    [Theory]
    [InlineData("2027-01", "2027-03", "posle 2027-01 očekuje se 2027-02")] // gap
    [InlineData("2027-02", "2027-01", "posle 2027-02 očekuje se 2027-03")] // not chronological
    [InlineData("2027-01", "2027-01", "posle 2027-01 očekuje se 2027-02")] // repeated
    public async Task Months_must_be_chronological_without_gaps(string first, string second, string expected)
    {
        var (t, cycleId, mikula) = await SetupAsync();
        await using var _ = t;
        var file = Samples.SkuUpload(sheet =>
        {
            sheet.Range(1, 4, 1, 7).Clear(); // keep two month columns
            sheet.Cell(1, 2).Value = first;
            sheet.Cell(1, 3).Value = second;
        });

        var ex = await Assert.ThrowsAsync<DomainException>(() => Import(t, mikula, cycleId, file));
        Assert.Contains(ex.Errors, e => e.Contains(expected));
    }

    [Theory]
    [InlineData("2027-1")]
    [InlineData("2027-13")]
    [InlineData("jan 2027")]
    [InlineData("Total")]
    public async Task Month_header_must_be_yyyy_mm(string header)
    {
        var (t, cycleId, mikula) = await SetupAsync();
        await using var _ = t;
        var file = Samples.SkuUpload(sheet => sheet.Cell(1, 8).Value = header);

        var ex = await Assert.ThrowsAsync<DomainException>(() => Import(t, mikula, cycleId, file));
        Assert.Contains(ex.Errors, e => e.Contains("YYYY-MM"));
    }

    [Fact]
    public async Task Month_header_typed_as_excel_date_is_accepted()
    {
        var (t, cycleId, mikula) = await SetupAsync();
        await using var _ = t;
        var file = Samples.SkuUpload(sheet => sheet.Cell(1, 2).Value = new DateTime(2027, 1, 1));

        var result = await Import(t, mikula, cycleId, file);
        Assert.Equal(8, result.ImportedCount);
    }

    [Fact]
    public async Task First_header_cell_must_be_sku()
    {
        var (t, cycleId, mikula) = await SetupAsync();
        await using var _ = t;
        var file = Samples.SkuUpload(sheet => sheet.Cell(1, 1).Value = "Šifra");

        await Assert.ThrowsAsync<DomainException>(() => Import(t, mikula, cycleId, file));
    }

    [Fact]
    public async Task Row_with_values_but_no_code_is_rejected()
    {
        var (t, cycleId, mikula) = await SetupAsync();
        await using var _ = t;
        var file = Samples.SkuUpload(sheet => sheet.Cell(10, 3).Value = 100);

        var ex = await Assert.ThrowsAsync<DomainException>(() => Import(t, mikula, cycleId, file));
        Assert.Contains("Red 10: šifra je prazna.", ex.Errors);
    }

    [Fact]
    public async Task Non_xlsx_content_is_rejected()
    {
        var (t, cycleId, mikula) = await SetupAsync();
        await using var _ = t;

        await Assert.ThrowsAsync<DomainException>(() => Import(t, mikula, cycleId, "SKU,2027-01\nTX-1,"u8.ToArray()));
    }

    [Theory]
    [InlineData("tijana")]
    [InlineData("milan")]
    [InlineData("pop")]
    public async Task Only_procurement_may_upload(string actor)
    {
        var (t, cycleId, _) = await SetupAsync();
        await using var __ = t;

        var actorId = await t.IdOf(actor);
        await Assert.ThrowsAsync<PermissionDeniedException>(() => Import(t, actorId, cycleId, Samples.SkuUpload()));
    }

    [Theory]
    [InlineData(CycleStatus.Locked)]
    [InlineData(CycleStatus.Finalized)]
    public async Task Upload_requires_open_cycle(CycleStatus status)
    {
        var (t, cycleId, mikula) = await SetupAsync();
        await using var _ = t;
        var db = t.Get<AppDbContext>();
        (await db.Cycles.SingleAsync(c => c.Id == cycleId)).Status = status;
        await db.SaveChangesAsync();

        await Assert.ThrowsAsync<DomainException>(() => Import(t, mikula, cycleId, Samples.SkuUpload()));
    }

    [Fact]
    public async Task Upload_after_deadline_is_rejected_even_while_cycle_is_open()
    {
        var (t, cycleId, mikula) = await SetupAsync();
        await using var _ = t;
        var db = t.Get<AppDbContext>();
        (await db.Cycles.SingleAsync(c => c.Id == cycleId)).Deadline = DateTime.UtcNow.AddMinutes(-1);
        await db.SaveChangesAsync();

        var ex = await Assert.ThrowsAsync<DomainException>(() => Import(t, mikula, cycleId, Samples.SkuUpload()));
        Assert.Contains(ex.Errors, e => e.Contains("Rok"));
    }

    [Fact]
    public async Task Upload_is_audited()
    {
        var (t, cycleId, mikula) = await SetupAsync();
        await using var _ = t;

        var result = await Import(t, mikula, cycleId, Samples.SkuUpload());

        var entry = await t.Get<AppDbContext>().AuditLogs.SingleAsync(a => a.Entity == "Batch");
        Assert.Equal(("Upload", result.BatchId.ToString(), mikula), (entry.Action, entry.EntityId, entry.UserId));
        Assert.Contains("\"Imported\":8", entry.NewValue);
    }
}
