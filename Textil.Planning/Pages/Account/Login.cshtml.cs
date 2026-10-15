using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Textil.Planning.Services;

namespace Textil.Planning.Pages.Account;

public class LoginModel(IAuthService auth) : PageModel
{
    [BindProperty] public string Login { get; set; } = "";
    [BindProperty] public string Password { get; set; } = "";
    [BindProperty] public bool RememberMe { get; set; }
    [BindProperty(SupportsGet = true)] public string? ReturnUrl { get; set; }

    public string? Error { get; private set; }

    public async Task<IActionResult> OnPostAsync()
    {
        var outcome = await auth.SignInAsync(Login, Password, RememberMe);
        switch (outcome)
        {
            case SignInOutcome.Success:
                return LocalRedirect(Url.IsLocalUrl(ReturnUrl) ? ReturnUrl : "/");
            case SignInOutcome.LockedOut:
                Error = "Nalog je privremeno zaključan zbog više neuspešnih pokušaja. Pokušajte ponovo za 10 minuta.";
                break;
            default:
                Error = "Pogrešno korisničko ime ili lozinka.";
                break;
        }
        Password = "";
        return Page();
    }
}
