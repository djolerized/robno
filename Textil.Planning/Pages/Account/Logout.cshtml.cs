using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Textil.Planning.Services;

namespace Textil.Planning.Pages.Account;

public class LogoutModel(IAuthService auth) : PageModel
{
    public IActionResult OnGet() => RedirectToPage("/Index");

    public async Task<IActionResult> OnPostAsync()
    {
        await auth.SignOutAsync();
        return RedirectToPage("/Account/Login");
    }
}
