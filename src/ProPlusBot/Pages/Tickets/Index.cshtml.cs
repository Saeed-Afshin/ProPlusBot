using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using ProPlusBot.Auth;
using ProPlusBot.Entities;
using ProPlusBot.Models;
using ProPlusBot.Services.Tickets;

namespace ProPlusBot.Pages.Tickets;

[Authorize(AuthenticationSchemes = AuthConstants.Scheme)]
public class IndexModel(TicketAdminService ticketAdmin) : PageModel
{
    [BindProperty(SupportsGet = true)]
    public string? StatusFilter { get; set; }

    [BindProperty(SupportsGet = true)]
    public TicketListSort SortOrder { get; set; } = TicketListSort.Priority;

    public List<SupportTicketListItemDto> Tickets { get; set; } = [];
    public string? SuccessMessage { get; set; }
    public string? ErrorMessage { get; set; }
    public List<SelectListItem> StatusFilterItems { get; private set; } = [];
    public List<SelectListItem> SortOrderItems { get; private set; } = [];

    public async Task<IActionResult> OnGetAsync(CancellationToken ct)
    {
        if (!User.CanAccessAdminPanel())
            return RedirectToPage("/Login");

        SuccessMessage = TempData["SuccessMessage"] as string ?? SuccessMessage;
        ErrorMessage = TempData["ErrorMessage"] as string ?? ErrorMessage;

        StatusFilterItems =
        [
            new SelectListItem("همه", ""),
            new SelectListItem(TicketStatusRules.ToDisplayName(SupportTicketStatus.Created), nameof(SupportTicketStatus.Created)),
            new SelectListItem(TicketStatusRules.ToDisplayName(SupportTicketStatus.WaitingForAdmin), nameof(SupportTicketStatus.WaitingForAdmin)),
            new SelectListItem(TicketStatusRules.ToDisplayName(SupportTicketStatus.WaitingForUser), nameof(SupportTicketStatus.WaitingForUser)),
            new SelectListItem(TicketStatusRules.ToDisplayName(SupportTicketStatus.Closed), nameof(SupportTicketStatus.Closed))
        ];

        SortOrderItems =
        [
            new SelectListItem("اولویت پشتیبانی (پیش‌فرض)", TicketListSort.Priority.ToString()),
            new SelectListItem("آخرین به‌روزرسانی (قدیمی‌ترین)", TicketListSort.UpdatedAsc.ToString()),
            new SelectListItem("آخرین به‌روزرسانی (جدیدترین)", TicketListSort.UpdatedDesc.ToString()),
            new SelectListItem("تاریخ ایجاد (قدیمی‌ترین)", TicketListSort.CreatedAsc.ToString()),
            new SelectListItem("تاریخ ایجاد (جدیدترین)", TicketListSort.CreatedDesc.ToString())
        ];

        SupportTicketStatus? filter = null;
        if (Enum.TryParse<SupportTicketStatus>(StatusFilter, out var parsed))
            filter = parsed;

        Tickets = await ticketAdmin.ListAsync(filter, SortOrder, ct);
        return Page();
    }
}
