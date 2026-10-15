using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Textil.Planning.Domain;
using Textil.Planning.Services;

namespace Textil.Planning.Pages.Batches;

/// <summary>Downloads the original xlsx of a batch.</summary>
public class FileModel(CycleService cycles, ICurrentUser currentUser) : PageModel
{
    private const string XlsxContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

    public async Task<IActionResult> OnGetAsync(int id)
    {
        try
        {
            var file = await cycles.GetOriginalFileAsync(currentUser.Id, id);
            return File(file.Content, XlsxContentType, file.FileName);
        }
        catch (PermissionDeniedException)
        {
            return Forbid();
        }
        catch (DomainException)
        {
            return NotFound();
        }
    }
}
