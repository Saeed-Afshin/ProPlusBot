namespace ProPlusBot.Services.Subscriptions;

/// <summary>Prices are stored and shown in Toman; Bale invoices use Rials (×10).</summary>
public static class TomanCurrency
{
    public const int RialsPerToman = 10;

    public static long ToRials(long toman) => checked(toman * RialsPerToman);

    public static long FromRials(long rials) => rials / RialsPerToman;

    public static string FormatToman(long toman) => $"{toman:N0} تومان";

    public static int ToInvoiceAmount(long toman)
    {
        var rials = ToRials(toman);
        if (rials > int.MaxValue)
            throw new InvalidOperationException("مبلغ برای فاکتور بیش از حد مجاز است.");

        return (int)rials;
    }
}
