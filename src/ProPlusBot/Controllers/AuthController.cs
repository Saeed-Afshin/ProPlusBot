using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using ProPlusBot.Auth;
using ProPlusBot.Configuration;
using ProPlusBot.Entities;
using ProPlusBot.Models;
using ProPlusBot.Services;

namespace ProPlusBot.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController(
    OtpService otpService,
    AdminJwtTokenService jwtTokenService,
    IOptions<JwtOptions> jwtOptions) : ControllerBase
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

        var claims = AdminAuthHelper.BuildClaims(admin);
        var token = jwtTokenService.CreateToken(claims);
        AdminAuthHelper.SetAuthCookie(Response, Request, token, jwtOptions.Value);

        return Ok(new
        {
            token,
            admin.Id,
            admin.DisplayName,
            admin.Role,
            admin.IsConfigSuperAdmin
        });
    }

    [HttpPost("logout")]
    [Authorize(Policy = AuthConstants.Scheme)]
    public IActionResult Logout()
    {
        AdminAuthHelper.ClearAuthCookie(Response);
        return Ok(new { message = "خروج انجام شد." });
    }
}
