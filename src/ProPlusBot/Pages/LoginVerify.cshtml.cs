using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using ProPlusBot.Auth;
using ProPlusBot.Entities;
using ProPlusBot.Services;

namespace ProPlusBot.Pages;

public class LoginVerifyModel(OtpService otpService) : PageModel
{
    [BindProperty]
    public string PhoneNumber { get; set; } = string.Empty;

    [BindProperty]
    public string Code { get; set; } = string.Empty;

    public string? ErrorMessage { get; set; }

    public IActionResult OnGet()
    {
        PhoneNumber = TempData["Phone"]?.ToString() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(PhoneNumber))
            return RedirectToPage("/Login");
        TempData.Keep("Phone");
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(PhoneNumber))
            PhoneNumber = TempData["Phone"]?.ToString() ?? string.Empty;

        var admin = await otpService.ValidateOtpAsync(PhoneNumber, Code, ct);
        if (admin is null)
        {
            ErrorMessage = "کد نامعتبر یا منقضی شده است.";
            return Page();
        }

        if (admin.Role == AdminRole.Tester)
        {
            ErrorMessage = "تسترها اجازه ورود به پنل را ندارند.";
            return Page();
        }

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, admin.TelegramUserId.ToString()),
            new(ClaimTypes.Name, admin.DisplayName ?? admin.PhoneNumber),
            new(AuthConstants.RoleClaim, admin.Role.ToString())
        };

        if (admin.Id.HasValue)
            claims.Add(new Claim(AuthConstants.AdminIdClaim, admin.Id.Value.ToString()));

        if (admin.IsConfigSuperAdmin)
            claims.Add(new Claim(AuthConstants.ConfigSuperAdminClaim, "true"));

        await HttpContext.SignInAsync(
            AuthConstants.Scheme,
            new ClaimsPrincipal(new ClaimsIdentity(claims, AuthConstants.Scheme)));

        return RedirectToPage("/Index");
    }
}
