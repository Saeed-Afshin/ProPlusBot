using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using ProPlusBot.Services;

namespace ProPlusBot.Pages;

public class LoginModel(OtpService otpService) : PageModel
{
    [BindProperty]
    public string PhoneNumber { get; set; } = string.Empty;

    public string? ErrorMessage { get; set; }

    public void OnGet()
    {
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken ct)
    {
        var (success, error) = await otpService.SendLoginOtpAsync(PhoneNumber, ct);
        if (!success)
        {
            ErrorMessage = error;
            return Page();
        }

        TempData["Phone"] = PhoneNumber;
        return RedirectToPage("/LoginVerify");
    }
}
