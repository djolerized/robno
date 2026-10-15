using Microsoft.AspNetCore.Mvc.RazorPages;
using Textil.Planning.Domain;
using Textil.Planning.Services;

namespace Textil.Planning.Pages.Admin.Users;

public class IndexModel(UserAdminService admin, ICurrentUser currentUser) : PageModel
{
    public IReadOnlyList<UserSummary> Users { get; private set; } = [];

    public async Task OnGetAsync()
    {
        Users = await admin.ListAsync(currentUser.Id);
    }

    public string NameOf(string? userId) => Users.FirstOrDefault(u => u.Id == userId)?.DisplayName ?? "—";

    public IEnumerable<UserSummary> ByRole(string role) => Users.Where(u => u.Roles.Contains(role));

    public IEnumerable<UserSummary> SubordinatesOf(string userId) => Users.Where(u => u.ManagerId == userId);
}
