using Microsoft.EntityFrameworkCore;
using Textil.Planning.Data;
using Textil.Planning.Services;

namespace Textil.Planning.Domain;

public sealed record ImportResult(
    int BatchId, int BatchNumber, int ImportedCount,
    IReadOnlyList<string> DuplicatesInFile, IReadOnlyList<string> DuplicatesInCycle);

/// <summary>Turns a procurement xlsx into a new batch of the cycle.</summary>
public class ImportService(AppDbContext db, AccessGuard access, IAuditService audit, TimeProvider clock)
{
    public const int MaxFileBytes = 5 * 1024 * 1024;

    public async Task<ImportResult> ImportAsync(string actorId, int cycleId, string fileName, byte[] content)
    {
        await access.EnsureAnyRoleAsync(actorId, Roles.Procurement);
        var cycle = await db.Cycles.Include(c => c.Months).SingleOrDefaultAsync(c => c.Id == cycleId)
            ?? throw new DomainException("Ciklus ne postoji.");
        var now = clock.GetUtcNow().UtcDateTime;
        CycleRules.EnsureWritable(cycle, now);

        if (content.Length == 0)
            throw new DomainException("Fajl je prazan.");
        if (content.Length > MaxFileBytes)
            throw new DomainException($"Fajl je veći od {MaxFileBytes / 1024 / 1024} MB.");
        fileName = Path.GetFileName(fileName.Trim());
        if (fileName.Length is 0 or > 260)
            throw new DomainException("Naziv fajla nije ispravan.");

        var file = SkuFileParser.Parse(new MemoryStream(content));
        var cycleMonths = cycle.Months.OrderBy(m => m.Order).Select(m => m.Month).ToList();
        if (cycleMonths.Count > 0)
            EnsureSameMonths(cycleMonths, file.Months);

        // Case-insensitive, like the default SQL Server collation behind the unique (CycleId, Code) index.
        var existing = (await db.CycleSkus.Where(s => s.CycleId == cycleId).Select(s => s.Code).ToListAsync())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var duplicatesInFile = new List<string>();
        var duplicatesInCycle = new List<string>();
        var codes = new List<string>();
        foreach (var code in file.Codes)
        {
            if (existing.Contains(code))
                duplicatesInCycle.Add(code);
            else if (!seen.Add(code))
                duplicatesInFile.Add(code);
            else
                codes.Add(code);
        }
        if (codes.Count == 0)
            throw new DomainException("Fajl ne donosi nijednu novu šifru — sve šifre već postoje u ciklusu.");

        var lastNumber = await db.Batches.Where(b => b.CycleId == cycleId).MaxAsync(b => (int?)b.Number) ?? 0;
        var lastOrder = await db.CycleSkus.Where(s => s.CycleId == cycleId).MaxAsync(s => (int?)s.Order) ?? 0;
        var plannerIds = await access.UserIdsInRoleAsync(Roles.Sales);

        if (cycleMonths.Count == 0)
            cycle.Months.AddRange(file.Months.Select((m, i) => new CycleMonth { CycleId = cycleId, Month = m, Order = i + 1 }));

        var batch = new Batch
        {
            CycleId = cycleId,
            Number = lastNumber + 1,
            UploadedById = actorId,
            UploadedAt = now,
            OriginalFileName = fileName,
            OriginalFile = content,
            Skus = codes.Select((code, i) => new CycleSku { CycleId = cycleId, Code = code, Order = lastOrder + i + 1 }).ToList(),
            Approvals = plannerIds.Select(id => new Approval { UserId = id, Status = ApprovalStatus.Draft, ChangedAt = now }).ToList(),
        };

        await using var tx = await db.Database.BeginTransactionAsync();
        db.Batches.Add(batch);
        await db.SaveChangesAsync();
        audit.Add("Batch", batch.Id.ToString(), "Upload", actorId, null, new
        {
            CycleId = cycleId,
            batch.Number,
            FileName = fileName,
            Imported = codes.Count,
            DuplicatesInFile = duplicatesInFile,
            DuplicatesInCycle = duplicatesInCycle,
        });
        await db.SaveChangesAsync();
        await tx.CommitAsync();

        return new ImportResult(batch.Id, batch.Number, codes.Count, duplicatesInFile, duplicatesInCycle);
    }

    private static void EnsureSameMonths(IReadOnlyList<string> cycleMonths, IReadOnlyList<string> fileMonths)
    {
        if (cycleMonths.SequenceEqual(fileMonths))
            return;

        var errors = new List<string>
        {
            $"Meseci u fajlu se ne poklapaju sa mesecima ciklusa ({cycleMonths[0]} … {cycleMonths[^1]}).",
        };
        var missing = cycleMonths.Except(fileMonths).ToList();
        var extra = fileMonths.Except(cycleMonths).ToList();
        if (missing.Count > 0)
            errors.Add("Nedostaju: " + string.Join(", ", missing) + ".");
        if (extra.Count > 0)
            errors.Add("Višak: " + string.Join(", ", extra) + ".");
        throw new DomainException(errors);
    }
}
