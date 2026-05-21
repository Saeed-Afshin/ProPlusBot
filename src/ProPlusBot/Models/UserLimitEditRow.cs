using ProPlusBot.Entities;

namespace ProPlusBot.Models;

public record UserLimitEditRow(
    MediaPlatformKind Platform,
    UsagePeriod Period,
    QuotaLimitKind LimitKind,
    decimal DisplayValue,
    bool IsCustom);
