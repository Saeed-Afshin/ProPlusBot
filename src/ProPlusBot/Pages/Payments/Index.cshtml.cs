using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;
using ProPlusBot.Auth;
using ProPlusBot.Entities;
using ProPlusBot.Services.Subscriptions;

namespace ProPlusBot.Pages.Payments;

[Authorize(AuthenticationSchemes = AuthConstants.Scheme)]
public class IndexModel(SubscriptionAdminService adminService) : PageModel
{
    public List<Models.PaymentRecordDto> Payments { get; set; } = [];

    public async Task OnGetAsync(CancellationToken ct)
    {
        if (!User.CanAccessAdminPanel())
            return;

        Payments = await adminService.ListPaymentsAsync(ct: ct);
    }

    public static string StatusLabel(PaymentStatus status) => status switch
    {
        PaymentStatus.Pending => "در انتظار",
        PaymentStatus.Completed => "موفق",
        PaymentStatus.Failed => "ناموفق",
        PaymentStatus.Cancelled => "لغو",
        _ => status.ToString()
    };

    public static string TypeLabel(PaymentType type) => type switch
    {
        PaymentType.PlanUpgrade => "ارتقای بسته",
        PaymentType.PlanPurchase => "خرید بسته",
        PaymentType.ExtraQuota => "سهمیه اضافه (قدیمی)",
        PaymentType.ExtraDownloadPack => "سهمیه اضافه",
        PaymentType.ExtraDownloadCount => "تعداد دانلود اضافه",
        PaymentType.ExtraDownloadBytes => "حجم دانلود اضافه",
        _ => type.ToString()
    };
}
