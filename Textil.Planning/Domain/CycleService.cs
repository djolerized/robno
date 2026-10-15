using Microsoft.EntityFrameworkCore;
using Textil.Planning.Data;
using Textil.Planning.Services;

namespace Textil.Planning.Domain;

public sealed record CycleSummary(
    int Id, string Name, CycleStatus Status, DateTime Deadline, IReadOnlyList<string> Months, int BatchCount, int SkuCount);

public sealed record BatchSummary(
    int Id, int Number, DateTime UploadedAt, string UploadedBy, string OriginalFileName, IReadOnlyList<string> Codes);

public sealed record BatchFile(string FileName, byte[] Content);

public class CycleService(AppDbContext db, AccessGuard access, IAuditService audit, TimeProvider clock)
{
    public const int NameMaxLength = 100;

    public async Task<int> CreateAsync(string actorId, string name, DateTime deadlineUtc)
    {
        await access.EnsureAnyRoleAsync(actorId, Roles.Director);
        name = name.Trim();
        var now = clock.GetUtcNow().UtcDateTime;

        var errors = new List<string>();
        if (name.Length == 0)
            errors.Add("Naziv ciklusa je obavezan.");
        else if (name.Length > NameMaxLength)
            errors.Add($"Naziv ciklusa može imati najviše {NameMaxLength} znakova.");
        else if (await db.Cycles.AnyAsync(c => c.Name == name))
            errors.Add($"Ciklus \"{name}\" već postoji.");
        if (deadlineUtc <= now)
            errors.Add("Rok mora biti u budućnosti.");
        if (errors.Count > 0)
            throw new DomainException(errors);

        var cycle = new Cycle { Name = name, Deadline = deadlineUtc, CreatedById = actorId, CreatedAt = now };
        await using var tx = await db.Database.BeginTransactionAsync();
        db.Cycles.Add(cycle);
        await db.SaveChangesAsync();
        audit.Add("Cycle", cycle.Id.ToString(), "Create", actorId, null, new { cycle.Name, cycle.Deadline });
        await db.SaveChangesAsync();
        await tx.CommitAsync();
        return cycle.Id;
    }

    public async Task ChangeDeadlineAsync(string actorId, int cycleId, DateTime deadlineUtc)
    {
        await access.EnsureAnyRoleAsync(actorId, Roles.Director);
        var cycle = await db.Cycles.SingleOrDefaultAsync(c => c.Id == cycleId) ?? throw new DomainException("Ciklus ne postoji.");
        if (cycle.Status != CycleStatus.Open)
            throw new DomainException("Rok može da se menja samo dok je ciklus otvoren.");
        if (deadlineUtc <= clock.GetUtcNow().UtcDateTime)
            throw new DomainException("Rok mora biti u budućnosti.");

        audit.Add("Cycle", cycle.Id.ToString(), "ChangeDeadline", actorId, new { cycle.Deadline }, new { Deadline = deadlineUtc });
        cycle.Deadline = deadlineUtc;
        await db.SaveChangesAsync();
    }

    /// <summary>All cycles, newest first. Visible to every signed-in user.</summary>
    public async Task<IReadOnlyList<CycleSummary>> ListAsync()
    {
        var cycles = await db.Cycles.AsNoTracking()
            .OrderByDescending(c => c.CreatedAt)
            .Select(c => new
            {
                c.Id, c.Name, c.Status, c.Deadline,
                Months = c.Months.OrderBy(m => m.Order).Select(m => m.Month).ToList(),
                BatchCount = c.Batches.Count,
                SkuCount = db.CycleSkus.Count(s => s.CycleId == c.Id),
            })
            .ToListAsync();
        return cycles.Select(c => new CycleSummary(c.Id, c.Name, c.Status, c.Deadline, c.Months, c.BatchCount, c.SkuCount)).ToList();
    }

    public async Task<IReadOnlyList<BatchSummary>> ListBatchesAsync(string actorId, int cycleId)
    {
        await access.EnsureAnyRoleAsync(actorId, Roles.Procurement, Roles.TeamLead, Roles.Director);
        var batches = await db.Batches.AsNoTracking()
            .Where(b => b.CycleId == cycleId)
            .OrderBy(b => b.Number)
            .Select(b => new
            {
                b.Id, b.Number, b.UploadedAt, UploadedBy = b.UploadedBy!.DisplayName, b.OriginalFileName,
                Codes = b.Skus.OrderBy(s => s.Order).Select(s => s.Code).ToList(),
            })
            .ToListAsync();
        return batches.Select(b => new BatchSummary(b.Id, b.Number, b.UploadedAt, b.UploadedBy, b.OriginalFileName, b.Codes)).ToList();
    }

    public async Task<BatchFile> GetOriginalFileAsync(string actorId, int batchId)
    {
        await access.EnsureAnyRoleAsync(actorId, Roles.Procurement, Roles.TeamLead, Roles.Director);
        return await db.Batches.AsNoTracking()
            .Where(b => b.Id == batchId)
            .Select(b => new BatchFile(b.OriginalFileName, b.OriginalFile))
            .SingleOrDefaultAsync()
            ?? throw new DomainException("Slanje ne postoji.");
    }
}
