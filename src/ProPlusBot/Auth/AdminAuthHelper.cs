using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using ProPlusBot.Configuration;
using ProPlusBot.Entities;
using ProPlusBot.Models;

namespace ProPlusBot.Auth;

public static class AdminAuthHelper
{
    public static List<Claim> BuildClaims(AuthenticatedAdmin admin)
    {
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

        return claims;
    }

    public static void SetAuthCookie(HttpResponse response, HttpRequest request, string jwt, JwtOptions options)
    {
        response.Cookies.Append(
            AuthConstants.JwtCookieName,
            jwt,
            new CookieOptions
            {
                HttpOnly = true,
                Secure = request.IsHttps,
                SameSite = SameSiteMode.Lax,
                Path = "/",
                Expires = DateTimeOffset.UtcNow.AddDays(options.ExpirationDays)
            });
    }

    public static void ClearAuthCookie(HttpResponse response)
    {
        response.Cookies.Delete(AuthConstants.JwtCookieName, new CookieOptions { Path = "/" });
    }
}
