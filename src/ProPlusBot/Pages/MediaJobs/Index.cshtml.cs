using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;
using ProPlusBot.Auth;
using ProPlusBot.Entities;
using ProPlusBot.Models;
using ProPlusBot.Services.Media;

namespace ProPlusBot.Pages.MediaJobs;

[Authorize(AuthenticationSchemes = AuthConstants.Scheme)]
public class IndexModel(MediaDownloadJobService jobService) : PageModel
{
    public List<MediaDownloadJobListItemDto> Jobs { get; set; } = [];

    public MediaDownloadJobStatus? StatusFilter { get; set; }
    public long? UserIdFilter { get; set; }

    public async Task OnGetAsync(MediaDownloadJobStatus? status, long? userId, CancellationToken ct)
    {
        if (!User.CanAccessAdminPanel())
            return;

        StatusFilter = status;
        UserIdFilter = userId;
        Jobs = await jobService.ListAsync(status, userId, ct: ct);
    }
}
