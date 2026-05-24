using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using ProPlusBot.Auth;
using ProPlusBot.Models;
using ProPlusBot.Services.Media;

namespace ProPlusBot.Pages.MediaJobs;

[Authorize(AuthenticationSchemes = AuthConstants.Scheme)]
public class DetailModel(MediaDownloadJobService jobService) : PageModel
{
    public MediaDownloadJobDetailDto? Job { get; set; }

    public async Task<IActionResult> OnGetAsync(Guid id, CancellationToken ct)
    {
        if (!User.CanAccessAdminPanel())
            return RedirectToPage("/Login");

        Job = await jobService.GetDetailAsync(id, ct);
        return Job is null ? NotFound() : Page();
    }
}
