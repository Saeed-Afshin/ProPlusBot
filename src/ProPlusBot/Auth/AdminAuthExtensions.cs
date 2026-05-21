using System.Security.Claims;
using ProPlusBot.Entities;

namespace ProPlusBot.Auth;

public static class AdminAuthExtensions
{
    public static Guid? GetAdminId(this ClaimsPrincipal user)
    {
        var value = user.FindFirstValue(AuthConstants.AdminIdClaim);
        return Guid.TryParse(value, out var id) ? id : null;
    }

    public static AdminRole? GetAdminRole(this ClaimsPrincipal user)
    {
        var value = user.FindFirstValue(AuthConstants.RoleClaim);
        return Enum.TryParse<AdminRole>(value, out var role) ? role : null;
    }

    public static bool IsConfigSuperAdmin(this ClaimsPrincipal user) =>
        user.HasClaim(AuthConstants.ConfigSuperAdminClaim, "true");

    public static bool CanAccessAdminPanel(this ClaimsPrincipal user)
    {
        var role = user.GetAdminRole();
        return role is AdminRole.SuperAdmin or AdminRole.Admin;
    }
}
