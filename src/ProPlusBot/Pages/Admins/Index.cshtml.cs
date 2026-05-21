using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using ProPlusBot.Auth;
using ProPlusBot.Entities;
using ProPlusBot.Models;
using ProPlusBot.Services;

namespace ProPlusBot.Pages.Admins;

[Authorize(AuthenticationSchemes = AuthConstants.Scheme)]
public class IndexModel(AdminManagementService adminService) : PageModel
{
    public List<AdminUserDto> Admins { get; set; } = [];

    [BindProperty]
    public long NewTelegramUserId { get; set; }

    [BindProperty]
    public string NewPhoneNumber { get; set; } = string.Empty;

    [BindProperty]
    public string? NewDisplayName { get; set; }

    [BindProperty]
    public AdminRole NewRole { get; set; } = AdminRole.Tester;

    public string? ErrorMessage { get; set; }
    public string? SuccessMessage { get; set; }

    public async Task<IActionResult> OnGetAsync(CancellationToken ct)
    {
        if (!User.CanAccessAdminPanel())
            return RedirectToPage("/Login");

        Admins = await adminService.ListAsync(ct);
        return Page();
    }

    public async Task<IActionResult> OnPostAddAsync(CancellationToken ct)
    {
        if (!User.CanAccessAdminPanel())
            return RedirectToPage("/Login");

        var role = User.GetAdminRole();
        if (role is null)
            return RedirectToPage("/Login");

        try
        {
            await adminService.CreateAsync(
                new CreateAdminRequest(NewTelegramUserId, NewPhoneNumber, NewDisplayName, NewRole),
                role.Value,
                ct);
            SuccessMessage = "کاربر اضافه شد.";
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }

        Admins = await adminService.ListAsync(ct);
        return Page();
    }

    public async Task<IActionResult> OnPostDeactivateAsync(Guid id, CancellationToken ct)
    {
        if (!User.CanAccessAdminPanel())
            return RedirectToPage("/Login");

        var role = User.GetAdminRole();
        if (role is null)
            return RedirectToPage("/Login");

        try
        {
            await adminService.DeactivateAsync(id, role.Value, ct);
            SuccessMessage = "کاربر غیرفعال شد.";
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }

        Admins = await adminService.ListAsync(ct);
        return Page();
    }
}
