using Microsoft.EntityFrameworkCore;
using ProPlusBot.Data;
using ProPlusBot.Entities;
using ProPlusBot.Models;

namespace ProPlusBot.Services;

public class AdminManagementService(AppDbContext db, RoleResolverService roleResolver)
{
    public async Task<List<AdminUserDto>> ListAsync(CancellationToken ct = default) =>
        await db.AdminUsers.AsNoTracking()
            .OrderBy(a => a.Role)
            .ThenBy(a => a.DisplayName)
            .Select(a => new AdminUserDto(a.Id, a.TelegramUserId, a.PhoneNumber, a.DisplayName, a.Role, a.IsActive))
            .ToListAsync(ct);

    public async Task<AdminUserDto?> CreateAsync(CreateAdminRequest request, AdminRole callerRole, CancellationToken ct = default)
    {
        if (callerRole == AdminRole.Tester)
            throw new UnauthorizedAccessException("Testers cannot manage admins.");

        if (request.Role == AdminRole.SuperAdmin)
            throw new InvalidOperationException("سوپر ادمین فقط از طریق appsettings تعریف می‌شود.");

        if (request.Role == AdminRole.Tester && callerRole == AdminRole.Admin)
        {
            // admins can add testers
        }
        else if (request.Role == AdminRole.Admin && callerRole != AdminRole.SuperAdmin)
            throw new UnauthorizedAccessException("Only SuperAdmin can add admins.");

        var phone = PhoneNormalizer.Normalize(request.PhoneNumber);
        if (roleResolver.IsSuperAdminPhone(phone))
            throw new InvalidOperationException("این شماره برای سوپر ادمین در تنظیمات رزرو شده است.");

        if (await db.AdminUsers.AnyAsync(a => a.PhoneNumber == phone || a.TelegramUserId == request.TelegramUserId, ct))
            throw new InvalidOperationException("Admin already exists.");

        var entity = new AdminUser
        {
            Id = Guid.NewGuid(),
            TelegramUserId = request.TelegramUserId,
            PhoneNumber = phone,
            DisplayName = request.DisplayName,
            Role = request.Role,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };
        db.AdminUsers.Add(entity);
        await db.SaveChangesAsync(ct);
        return new AdminUserDto(entity.Id, entity.TelegramUserId, entity.PhoneNumber, entity.DisplayName, entity.Role, entity.IsActive);
    }

    public async Task<bool> DeactivateAsync(Guid id, AdminRole callerRole, CancellationToken ct = default)
    {
        var admin = await db.AdminUsers.FirstOrDefaultAsync(a => a.Id == id, ct);
        if (admin is null)
            return false;

        if (callerRole == AdminRole.Admin && admin.Role != AdminRole.Tester)
            throw new UnauthorizedAccessException("Admins can only deactivate testers.");

        admin.IsActive = false;
        admin.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return true;
    }
}
