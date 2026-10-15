using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Textil.Planning.Domain;
using Textil.Planning.Services;

namespace Textil.Planning.Pages.Director.Cycles;

public class IndexModel(CycleService cycles, ICurrentUser currentUser, LocalTime time, TimeProvider clock) : PageModel
{
    public LocalTime Time => time;
    public IReadOnlyList<CycleSummary> Cycles { get; private set; } = [];

    [BindProperty] public string NewName { get; set; } = "";
    [BindProperty] public DateTime? NewDeadline { get; set; }

    public async Task OnGetAsync()
    {
        NewDeadline ??= time.Now(clock).Date.AddDays(14).AddHours(16);
        Cycles = await cycles.ListAsync();
    }

    public async Task<IActionResult> OnPostCreateAsync()
    {
        if (NewDeadline is null)
        {
            TempData["Error"] = "Rok je obavezan.";
            return RedirectToPage();
        }
        try
        {
            await cycles.CreateAsync(currentUser.Id, NewName, time.ToUtc(NewDeadline.Value));
            TempData["Message"] = $"Ciklus \"{NewName.Trim()}\" je kreiran. Nabavka sada može da pošalje SKU.";
        }
        catch (DomainException ex)
        {
            TempData.SetErrors(ex);
        }
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostDeadlineAsync(int cycleId, DateTime? deadline)
    {
        if (deadline is null)
        {
            TempData["Error"] = "Rok je obavezan.";
            return RedirectToPage();
        }
        try
        {
            await cycles.ChangeDeadlineAsync(currentUser.Id, cycleId, time.ToUtc(deadline.Value));
            TempData["Message"] = $"Rok je promenjen na {deadline.Value:dd.MM.yyyy. HH:mm}.";
        }
        catch (DomainException ex)
        {
            TempData.SetErrors(ex);
        }
        return RedirectToPage();
    }
}
