namespace SaasTemplate.Api.Billing;

/// <summary>
/// Generic usage limits per subscription tier.
/// Replace MonthlyQuota and Seats with whatever limits your product needs.
/// </summary>
public static class TierLimits
{
    public static readonly TierConfig Free         = new(MonthlyQuota: 0,            Seats: 1);
    public static readonly TierConfig Starter      = new(MonthlyQuota: 100,          Seats: 1);
    public static readonly TierConfig Professional = new(MonthlyQuota: 1000,         Seats: 5);
    public static readonly TierConfig Business     = new(MonthlyQuota: int.MaxValue, Seats: 25);

    public static TierConfig ForTier(string? tier) => tier?.ToLowerInvariant() switch
    {
        "starter"      => Starter,
        "professional" => Professional,
        "business"     => Business,
        _              => Free
    };
}

public sealed record TierConfig(int MonthlyQuota, int Seats);
