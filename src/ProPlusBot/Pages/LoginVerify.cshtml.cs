using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Options;
using ProPlusBot.Auth;
using ProPlusBot.Configuration;
using ProPlusBot.Entities;
using ProPlusBot.Services;

namespace ProPlusBot.Pages;

public class LoginVerifyModel(
    OtpService otpService,
    AdminJwtTokenService jwtTokenService,
    IOptions<JwtOptions> jwtOptions) : PageModel
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

        var claims = AdminAuthHelper.BuildClaims(admin);
        var token = jwtTokenService.CreateToken(claims);
        AdminAuthHelper.SetAuthCookie(Response, Request, token, jwtOptions.Value);

        return RedirectToPage("/Index");
    }
}
