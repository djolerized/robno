using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Textil.Planning.Data;
using Textil.Planning.Domain;
using Textil.Planning.Services;

namespace Textil.Planning.Pages;

public class IndexModel(AppDbContext db, ICurrentUser currentUser, CycleService cycles, LocalTime time) : PageModel
{
    public LocalTime Time => time;
    public string DisplayName { get; private set; } = "";
    public string? ManagerName { get; private set; }
    public IReadOnlyList<string> Roles { get; private set; } = [];
    public IReadOnlyList<CycleSummary> OpenCycles { get; private set; } = [];

    public async Task OnGetAsync()
    {
        var me = await db.Users.AsNoTracking()
            .Where(u => u.Id == currentUser.Id)
            .Select(u => new { u.DisplayName, ManagerName = u.Manager != null ? u.Manager.DisplayName : null })
            .SingleAsync();
        DisplayName = me.DisplayName;
        ManagerName = me.ManagerName;
        Roles = Domain.Roles.All.Where(currentUser.IsInRole).ToList();
        OpenCycles = (await cycles.ListAsync()).Where(c => c.Status == CycleStatus.Open).ToList();
    }
}
