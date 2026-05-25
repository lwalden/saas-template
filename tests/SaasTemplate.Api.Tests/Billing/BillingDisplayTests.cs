using Xunit;

namespace SaasTemplate.Api.Tests.Billing;

/// <summary>
/// Tests for billing display logic — specifically the CurrentPeriodEnd rendering guard.
/// </summary>
public class BillingDisplayTests
{
    /// <summary>
    /// The same logic used in Billing.razor to decide whether to show the renewal date.
    /// Extracted here so it can be unit-tested without bUnit.
    /// </summary>
    private static bool ShouldShowRenewalDate(DateTime? currentPeriodEnd)
        => currentPeriodEnd.HasValue && currentPeriodEnd.Value.Year >= 2020;

    [Fact]
    public void Null_period_end_does_not_display()
    {
        Assert.False(ShouldShowRenewalDate(null));
    }

    [Fact]
    public void Default_DateTime_does_not_display()
    {
        // DateTime.MinValue = 0001-01-01 — the "January 1, 0001" bug
        Assert.False(ShouldShowRenewalDate(default(DateTime)));
    }

    [Fact]
    public void Unix_epoch_does_not_display()
    {
        // 1970-01-01 — the "January 1, 1970" bug from the report
        Assert.False(ShouldShowRenewalDate(new DateTime(1970, 1, 1)));
    }

    [Fact]
    public void Valid_future_date_displays()
    {
        Assert.True(ShouldShowRenewalDate(new DateTime(2026, 4, 15)));
    }

    [Fact]
    public void Valid_recent_past_date_displays()
    {
        // Expired subscription — still a real date worth showing
        Assert.True(ShouldShowRenewalDate(new DateTime(2026, 3, 1)));
    }

    /// <summary>
    /// S31-001: "Payment &amp; Cancellation" was split into separate
    /// "Manage Payment" and "Cancel Subscription" buttons. The old
    /// combined button no longer exists.
    /// </summary>
    [Fact]
    public void Portal_button_label_is_manage_payment()
    {
        // Walk up from test bin directory to solution root
        var dir = AppContext.BaseDirectory;
        while (dir is not null && !File.Exists(Path.Combine(dir, "SaasTemplate.sln")))
            dir = Directory.GetParent(dir)?.FullName;

        Assert.NotNull(dir);

        var razorPath = Path.Combine(dir!, "src", "SaasTemplate.Api",
            "Components", "Pages", "Billing.razor");
        var source = File.ReadAllText(razorPath);

        // Old combined button should be gone
        Assert.DoesNotContain("Payment & Cancellation", source);
        // New separate buttons should exist
        Assert.Contains("Manage Payment", source);
        Assert.Contains("Cancel Subscription", source);
    }
}
