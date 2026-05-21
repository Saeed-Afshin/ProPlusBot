namespace ProPlusBot.Entities;

public class BotSetting
{
    public int Id { get; set; } = 1;
    public BotMode Mode { get; set; } = BotMode.Live;
    public BotUpdateMode UpdateMode { get; set; } = BotUpdateMode.LongPolling;
    public bool IsActive { get; set; } = true;
    public string? WebhookUrl { get; set; }
    public DateTime UpdatedAt { get; set; }
    public Guid? UpdatedByAdminId { get; set; }
}
