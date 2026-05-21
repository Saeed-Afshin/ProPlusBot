namespace ProPlusBot.Entities;

public class AdminUser
{
    public Guid Id { get; set; }
    public long TelegramUserId { get; set; }
    public string PhoneNumber { get; set; } = string.Empty;
    public string? DisplayName { get; set; }
    public AdminRole Role { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
