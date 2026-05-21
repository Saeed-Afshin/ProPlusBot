using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ProPlusBot.Auth;
using ProPlusBot.Entities;
using ProPlusBot.Models;
using ProPlusBot.Services;

namespace ProPlusBot.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController(OtpService otpService) : ControllerBase
{
    [HttpPost("otp")]
    [AllowAnonymous]
    public async Task<IActionResult> SendOtp([FromBody] SendOtpRequest request, CancellationToken ct)
    {
        var (success, error) = await otpService.SendLoginOtpAsync(request.PhoneNumber, ct);
        return success ? Ok(new { message = "کد ورود از طریق ربات ارسال شد." }) : BadRequest(new { error });
    }

    [HttpPost("verify")]
    [AllowAnonymous]
    public async Task<IActionResult> Verify([FromBody] VerifyOtpRequest request, CancellationToken ct)
    {
        var admin = await otpService.ValidateOtpAsync(request.PhoneNumber, request.Code, ct);
        if (admin is null)
            return BadRequest(new { error = "کد نامعتبر یا منقضی شده است." });

        if (admin.Role == AdminRole.Tester)
            return BadRequest(new { error = "تسترها اجازه ورود به پنل را ندارند." });

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

        var identity = new ClaimsIdentity(claims, AuthConstants.Scheme);
        await HttpContext.SignInAsync(AuthConstants.Scheme, new ClaimsPrincipal(identity));

        return Ok(new { admin.Id, admin.DisplayName, admin.Role, admin.IsConfigSuperAdmin });
    }

    [HttpPost("logout")]
    [Authorize(Policy = AuthConstants.Scheme)]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(AuthConstants.Scheme);
        return Ok(new { message = "خروج انجام شد." });
    }
}
