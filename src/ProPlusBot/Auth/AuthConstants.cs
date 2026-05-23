namespace ProPlusBot.Auth;

public static class AuthConstants
{
    public const string Scheme = "ProPlusBot.Admin";
    public const string JwtCookieName = "proplus_admin_token";
    public const string RoleClaim = "admin_role";
    public const string AdminIdClaim = "admin_id";
    public const string ConfigSuperAdminClaim = "config_super_admin";
}
