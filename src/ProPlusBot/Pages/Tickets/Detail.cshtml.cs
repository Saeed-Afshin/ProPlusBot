using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using ProPlusBot.Auth;
using ProPlusBot.Entities;
using ProPlusBot.Models;
using ProPlusBot.Services.Tickets;

namespace ProPlusBot.Pages.Tickets;

[Authorize(AuthenticationSchemes = AuthConstants.Scheme)]
public class DetailModel(
    TicketAdminService ticketAdmin,
    TicketService ticketService,
    TicketNotificationService notifications) : PageModel
{
    [BindProperty(SupportsGet = true)]
    public Guid Id { get; set; }

    [BindProperty]
    public Guid TicketId { get; set; }

    [BindProperty]
    public string ReplyText { get; set; } = "";

    public SupportTicketDetailDto? Ticket { get; set; }
    public string? SuccessMessage { get; set; }
    public string? ErrorMessage { get; set; }

    public async Task<IActionResult> OnGetAsync(CancellationToken ct)
    {
        if (!User.CanAccessAdminPanel())
            return RedirectToPage("/Login");

        Ticket = await ticketAdmin.GetDetailAsync(Id, ct);
        if (Ticket is null)
            return NotFound();

        TicketId = Ticket.Id;
        return Page();
    }

    public async Task<IActionResult> OnPostReplyAsync(CancellationToken ct)
    {
        if (!User.CanAccessAdminPanel())
            return RedirectToPage("/Login");

        if (string.IsNullOrWhiteSpace(ReplyText))
        {
            ErrorMessage = "متن پاسخ خالی است.";
            return await ReloadAsync(ct);
        }

        var adminId = User.GetAdminId();
        var adminDisplayName = User.Identity?.Name;

        try
        {
            var detail = await ticketAdmin.GetDetailAsync(TicketId, ct)
                ?? throw new InvalidOperationException("تیکت یافت نشد.");

            if (!TicketStatusRules.IsOpen(detail.Status))
                throw new InvalidOperationException("تیکت بسته است.");

            await notifications.NotifyUserAsync(detail.TelegramUserId, ReplyText.Trim(), ct);
            await ticketService.AddAdminReplyAsync(TicketId, adminId, adminDisplayName, ReplyText, ct);

            TempData["SuccessMessage"] = "پاسخ ارسال شد.";
            return RedirectToPage("./Index");
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }

        return await ReloadAsync(ct);
    }

    public async Task<IActionResult> OnPostCloseAsync(CancellationToken ct)
    {
        if (!User.CanAccessAdminPanel())
            return RedirectToPage("/Login");

        try
        {
            var detail = await ticketAdmin.GetDetailAsync(TicketId, ct);
            await ticketService.CloseTicketAsync(TicketId, ct);
            if (detail is not null)
                await notifications.NotifyTicketClosedAsync(detail.TelegramUserId, ct);

            SuccessMessage = "تیکت بسته شد.";
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }

        return await ReloadAsync(ct);
    }

    private async Task<IActionResult> ReloadAsync(CancellationToken ct)
    {
        Ticket = await ticketAdmin.GetDetailAsync(TicketId, ct);
        if (Ticket is null)
            return NotFound();

        return Page();
    }
}
