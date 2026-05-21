using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using ProPlusBot.Configuration;
using ProPlusBot.Data;
using ProPlusBot.Entities;

namespace ProPlusBot.Services;

public class RoleResolverService(
    AppDbContext db,
    IOptions<SuperAdminOptions> superAdminOptions)
{
    public string? NormalizedSuperAdminPhone =>
        string.IsNullOrWhiteSpace(superAdminOptions.Value.PhoneNumber)
            ? null
            : PhoneNormalizer.Normalize(superAdminOptions.Value.PhoneNumber);

    public bool IsSuperAdminPhone(string phoneNumber)
    {
        var normalized = NormalizedSuperAdminPhone;
        return normalized is not null
               && PhoneNormalizer.Normalize(phoneNumber) == normalized;
    }

    public async Task<AdminRole?> ResolveRoleAsync(
        long telegramUserId,
        string phoneNumber,
        CancellationToken ct = default)
    {
        if (IsSuperAdminPhone(phoneNumber))
            return AdminRole.SuperAdmin;

        var normalized = PhoneNormalizer.Normalize(phoneNumber);
        var dbAdmin = await db.AdminUsers.AsNoTracking()
            .FirstOrDefaultAsync(
                a => a.IsActive && (a.PhoneNumber == normalized || a.TelegramUserId == telegramUserId),
                ct);

        return dbAdmin?.Role;
    }
}
