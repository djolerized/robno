using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Textil.Planning.Domain;
using Textil.Planning.Services;

namespace Textil.Planning.Pages.Admin.Users;

public class EditModel(UserAdminService admin, ICurrentUser currentUser) : PageModel
{
    [BindProperty(SupportsGet = true)] public string? Id { get; set; }
    [BindProperty] public UserForm Input { get; set; } = new();
    [BindProperty] public string UserName { get; set; } = "";
    [BindProperty] public string Password { get; set; } = "";

    public bool IsNew => Id is null;
    public List<SelectListItem> ManagerOptions { get; private set; } = [];

    public class UserForm
    {
        [Required(ErrorMessage = "Ime je obavezno.")]
        [StringLength(100, ErrorMessage = "Ime može imati najviše 100 znakova.")]
        public string DisplayName { get; set; } = "";

        [Required(ErrorMessage = "Email je obavezan.")]
        [EmailAddress(ErrorMessage = "Email nije ispravan.")]
        public string Email { get; set; } = "";

        public List<string> Roles { get; set; } = [];
        public string? ManagerId { get; set; }
    }

    public async Task<IActionResult> OnGetAsync()
    {
        if (!IsNew)
        {
            var user = await admin.GetAsync(currentUser.Id, Id!);
            UserName = user.UserName;
            Input = new UserForm
            {
                DisplayName = user.DisplayName,
                Email = user.Email ?? "",
                Roles = user.Roles.ToList(),
                ManagerId = user.ManagerId,
            };
        }
        await LoadManagerOptionsAsync();
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (IsNew && string.IsNullOrWhiteSpace(UserName))
            ModelState.AddModelError(nameof(UserName), "Korisničko ime je obavezno.");
        if (IsNew && string.IsNullOrEmpty(Password))
            ModelState.AddModelError(nameof(Password), "Početna lozinka je obavezna.");

        if (ModelState.IsValid)
        {
            var input = new UserInput(Input.DisplayName, Input.Email, Input.Roles, Input.ManagerId);
            try
            {
                if (IsNew)
                    await admin.CreateAsync(currentUser.Id, UserName, input, Password);
                else
                    await admin.UpdateAsync(currentUser.Id, Id!, input);

                TempData["Message"] = $"Korisnik {Input.DisplayName.Trim()} je sačuvan.";
                return RedirectToPage("Index");
            }
            catch (DomainException ex)
            {
                foreach (var error in ex.Errors)
                    ModelState.AddModelError("", error);
            }
        }

        if (!IsNew)
            UserName = (await admin.GetAsync(currentUser.Id, Id!)).UserName;
        await LoadManagerOptionsAsync();
        return Page();
    }

    public async Task<IActionResult> OnPostResetPasswordAsync(string newPassword)
    {
        try
        {
            await admin.ResetPasswordAsync(currentUser.Id, Id!, newPassword ?? "");
            TempData["Message"] = "Nova lozinka je postavljena.";
        }
        catch (DomainException ex)
        {
            TempData["Error"] = string.Join(" ", ex.Errors);
        }
        return RedirectToPage(new { id = Id });
    }

    private async Task LoadManagerOptionsAsync()
    {
        var users = await admin.ListAsync(currentUser.Id);
        ManagerOptions = users
            .Where(u => u.Id != Id && (u.Roles.Contains(Roles.TeamLead) || u.Roles.Contains(Roles.Director)))
            .Select(u => new SelectListItem(
                $"{u.DisplayName} ({Roles.Label(u.Roles.Contains(Roles.Director) ? Roles.Director : Roles.TeamLead).ToLowerInvariant()})",
                u.Id))
            .ToList();
    }
}
