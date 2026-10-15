using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Textil.Planning.Data;
using Textil.Planning.Domain;
using Textil.Planning.Services;

namespace Textil.Planning.Pages.Procurement;

public class IndexModel(
    CycleService cycles, ImportService import, ICurrentUser currentUser, LocalTime time, TimeProvider clock) : PageModel
{
    public LocalTime Time => time;
    public IReadOnlyList<CycleSummary> Cycles { get; private set; } = [];
    public CycleSummary? Cycle { get; private set; }
    public IReadOnlyList<BatchSummary> Batches { get; private set; } = [];
    public bool CanUpload { get; private set; }

    public async Task OnGetAsync(int? cycleId)
    {
        Cycles = await cycles.ListAsync();
        Cycle = Cycles.FirstOrDefault(c => c.Id == cycleId)
            ?? Cycles.FirstOrDefault(c => c.Status == CycleStatus.Open)
            ?? Cycles.FirstOrDefault();
        if (Cycle is null)
            return;

        Batches = await cycles.ListBatchesAsync(currentUser.Id, Cycle.Id);
        CanUpload = Cycle.Status == CycleStatus.Open && Cycle.Deadline > clock.GetUtcNow().UtcDateTime;
    }

    public async Task<IActionResult> OnPostUploadAsync(int cycleId, IFormFile? upload)
    {
        if (upload is null || upload.Length == 0)
        {
            TempData["Error"] = "Izaberite fajl.";
            return RedirectToPage(new { cycleId });
        }
        if (upload.Length > ImportService.MaxFileBytes)
        {
            TempData["Error"] = $"Fajl je veći od {ImportService.MaxFileBytes / 1024 / 1024} MB.";
            return RedirectToPage(new { cycleId });
        }

        using var buffer = new MemoryStream();
        await upload.CopyToAsync(buffer);
        try
        {
            var result = await import.ImportAsync(currentUser.Id, cycleId, upload.FileName, buffer.ToArray());
            TempData["Message"] = $"Slanje #{result.BatchNumber} je primljeno: {result.ImportedCount} SKU.";
            var warnings = new List<string>();
            if (result.DuplicatesInCycle.Count > 0)
                warnings.Add($"Preskočeno {result.DuplicatesInCycle.Count} šifara koje već postoje u ciklusu: {string.Join(", ", result.DuplicatesInCycle)}");
            if (result.DuplicatesInFile.Count > 0)
                warnings.Add($"Preskočeno {result.DuplicatesInFile.Count} šifara koje se ponavljaju u fajlu: {string.Join(", ", result.DuplicatesInFile)}");
            if (warnings.Count > 0)
                TempData["Warning"] = string.Join("\n", warnings);
        }
        catch (DomainException ex)
        {
            TempData.SetErrors(ex);
        }
        return RedirectToPage(new { cycleId });
    }
}
