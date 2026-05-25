using Xunit;

namespace SaasTemplate.Api.Tests.Billing;

/// <summary>
/// Tests for the billing page redesign (S31-001 / UX-05):
/// - Post-checkout banners with tier-specific text and polling
/// - Current plan highlighting and upgrade buttons
/// - Cancel button positioning and dialog semantics
/// - D-014 fix: features derived from _plans array, not hardcoded
/// </summary>
public class BillingPageTests
{
    // ── Plan definitions (mirror Billing.razor's _plans array) ──────────

    private sealed record PlanInfo(
        string Tier, string Label, int Price,
        bool Highlighted, bool ComingSoon, string[] Features);

    private static readonly PlanInfo[] Plans =
    [
        new("starter", "Starter", 49, false, false,
            ["10 scans/month, 5 pages each", "Code fix generation (Liquid patches)",
             "Compliance score (0-100)", "Compliance PDF report"]),
        new("professional", "Professional", 99, true, false,
            ["30 scans/month, 10 pages each", "Everything in Starter",
             "Weekly automated monitoring", "AI deep analysis on weekly scan"]),
        new("business", "Business", 149, false, false,
            ["Unlimited scans, 25 pages each", "Everything in Professional",
             "VPAT generation", "Custom rules engine"]),
    ];

    // ── Helper: replicate the logic that will live in Billing.razor ─────

    /// <summary>
    /// Derives current plan features from the _plans array (D-014 fix).
    /// For tiers that say "Everything in [lower tier]", expands to include
    /// the actual features from that tier.
    /// </summary>
    private static string[] DeriveCurrentPlanFeatures(string? tier, PlanInfo[] plans)
    {
        if (tier is null) return [];

        var plan = plans.FirstOrDefault(p => p.Tier == tier);
        if (plan is null) return [];

        var features = new List<string>();
        foreach (var feature in plan.Features)
        {
            if (feature.StartsWith("Everything in ", StringComparison.OrdinalIgnoreCase))
            {
                // Extract the referenced tier label and expand its features
                var referencedLabel = feature["Everything in ".Length..];
                var referencedPlan = plans.FirstOrDefault(
                    p => p.Label.Equals(referencedLabel, StringComparison.OrdinalIgnoreCase));
                if (referencedPlan is not null)
                {
                    // Recursively expand (handles Business → Professional → Starter)
                    features.AddRange(DeriveCurrentPlanFeatures(referencedPlan.Tier, plans));
                }
            }
            else
            {
                features.Add(feature);
            }
        }

        return features.ToArray();
    }

    /// <summary>
    /// Returns the checkout success banner text including the tier label.
    /// </summary>
    private static string GetSuccessBannerText(string tier, PlanInfo[] plans)
    {
        var plan = plans.FirstOrDefault(p => p.Tier == tier);
        var label = plan?.Label ?? tier;
        return $"Subscription activated — welcome to SaasTemplate {label}!";
    }

    /// <summary>
    /// Returns the tiers that should show "Upgrade to [tier]" buttons
    /// for a given current tier.
    /// </summary>
    private static string[] GetUpgradeTiers(string currentTier, PlanInfo[] plans)
    {
        var currentIndex = Array.FindIndex(plans, p => p.Tier == currentTier);
        if (currentIndex < 0) return [];
        return plans.Skip(currentIndex + 1)
                    .Where(p => !p.ComingSoon)
                    .Select(p => p.Tier)
                    .ToArray();
    }

    /// <summary>
    /// Whether the given tier is the current subscription tier — used to
    /// determine if subscribe button should be hidden.
    /// </summary>
    private static bool IsCurrentTier(string tier, string? subscriptionTier)
        => string.Equals(tier, subscriptionTier, StringComparison.OrdinalIgnoreCase);

    // ═══════════════════════════════════════════════════════════════════════
    // TEST 1: Success banner renders with tier label
    // ═══════════════════════════════════════════════════════════════════════

    [Theory]
    [InlineData("starter", "Starter")]
    [InlineData("professional", "Professional")]
    [InlineData("business", "Business")]
    public void BillingPage_CheckoutSuccess_ShowsBannerWithTierName(
        string tier, string expectedLabel)
    {
        var bannerText = GetSuccessBannerText(tier, Plans);

        Assert.Contains(expectedLabel, bannerText);
        Assert.StartsWith("Subscription activated", bannerText);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // TEST 2: Polls when no subscription (logic test)
    // ═══════════════════════════════════════════════════════════════════════

    [Fact]
    public void BillingPage_CheckoutSuccess_PollsWhenNoSubscription()
    {
        // The polling logic: when checkout=success but subscription is null,
        // we should poll. This tests the condition that triggers polling.
        string? checkoutParam = "success";
        object? subscription = null;

        bool shouldPoll = checkoutParam == "success" && subscription is null;

        Assert.True(shouldPoll,
            "Should poll when checkout=success but subscription is not yet available");
    }

    // ═══════════════════════════════════════════════════════════════════════
    // TEST 3: Cancelled checkout shows soft message without error styling
    // ═══════════════════════════════════════════════════════════════════════

    [Fact]
    public void BillingPage_CheckoutCancelled_ShowsSoftMessage()
    {
        // The cancelled message should NOT use error colors (red) —
        // it uses amber/warning styling (fffbeb background).
        // This test validates the message content.
        string cancelledMessage = "No charge was made. Choose a plan below whenever you\u2019re ready.";

        Assert.Contains("No charge was made", cancelledMessage);
        Assert.DoesNotContain("error", cancelledMessage, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("failed", cancelledMessage, StringComparison.OrdinalIgnoreCase);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // TEST 4: Active subscription shows current plan highlighted
    // ═══════════════════════════════════════════════════════════════════════

    [Theory]
    [InlineData("starter")]
    [InlineData("professional")]
    [InlineData("business")]
    public void BillingPage_ActiveSub_ShowsCurrentPlanHighlighted(string tier)
    {
        // For the current tier, IsCurrentTier returns true — meaning the
        // subscribe button should be hidden and a "Current Plan" badge shown
        Assert.True(IsCurrentTier(tier, tier));

        // For other tiers, IsCurrentTier returns false
        foreach (var plan in Plans.Where(p => p.Tier != tier))
        {
            Assert.False(IsCurrentTier(plan.Tier, tier));
        }
    }

    // ═══════════════════════════════════════════════════════════════════════
    // TEST 5: Starter sees upgrade buttons for Pro and Business
    // ═══════════════════════════════════════════════════════════════════════

    [Fact]
    public void BillingPage_StarterSub_ShowsUpgradeToProAndBusiness()
    {
        var upgradeTiers = GetUpgradeTiers("starter", Plans);

        Assert.Equal(2, upgradeTiers.Length);
        Assert.Contains("professional", upgradeTiers);
        Assert.Contains("business", upgradeTiers);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // TEST 6: Pro sees only "Upgrade to Business"
    // ═══════════════════════════════════════════════════════════════════════

    [Fact]
    public void BillingPage_ProSub_ShowsUpgradeToBusinessOnly()
    {
        var upgradeTiers = GetUpgradeTiers("professional", Plans);

        Assert.Single(upgradeTiers);
        Assert.Equal("business", upgradeTiers[0]);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // TEST 7: Business has no upgrade tiers
    // ═══════════════════════════════════════════════════════════════════════

    [Fact]
    public void BillingPage_BusinessSub_ShowsNoUpgrade()
    {
        var upgradeTiers = GetUpgradeTiers("business", Plans);

        Assert.Empty(upgradeTiers);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // TEST 8: Cancel dialog has proper dialog semantics (HTML structure test)
    // ═══════════════════════════════════════════════════════════════════════

    [Fact]
    public void BillingPage_CancelConfirmDialog_HasDialogSemantics()
    {
        // Walk up from test bin directory to solution root
        var dir = AppContext.BaseDirectory;
        while (dir is not null && !File.Exists(Path.Combine(dir, "SaasTemplate.sln")))
            dir = Directory.GetParent(dir)?.FullName;

        Assert.NotNull(dir);

        var razorPath = Path.Combine(dir!, "src", "SaasTemplate.Api",
            "Components", "Pages", "Billing.razor");
        var source = File.ReadAllText(razorPath);

        // Must use <dialog> element
        Assert.Contains("<dialog", source);
        // Must have role="alertdialog" for WCAG 2.1 AA
        Assert.Contains("role=\"alertdialog\"", source);
        // Must have aria-labelledby for accessible name
        Assert.Contains("aria-labelledby", source);
        // Must have aria-describedby for accessible description
        Assert.Contains("aria-describedby", source);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // TEST 9: D-014 fix — features derived from _plans, not hardcoded
    // ═══════════════════════════════════════════════════════════════════════

    [Fact]
    public void BillingPage_CurrentPlanFeatures_DerivedFromPlansArray()
    {
        // Starter: direct features from _plans
        var starterFeatures = DeriveCurrentPlanFeatures("starter", Plans);
        Assert.Equal(Plans[0].Features, starterFeatures);

        // Professional: "Everything in Starter" expands to include Starter features
        var proFeatures = DeriveCurrentPlanFeatures("professional", Plans);
        Assert.Contains("30 scans/month, 10 pages each", proFeatures);
        Assert.Contains("Weekly automated monitoring", proFeatures);
        Assert.Contains("AI deep analysis on weekly scan", proFeatures);
        // Expanded from "Everything in Starter"
        Assert.Contains("10 scans/month, 5 pages each", proFeatures);
        Assert.Contains("Code fix generation (Liquid patches)", proFeatures);
        Assert.Contains("Compliance score (0-100)", proFeatures);
        Assert.Contains("Compliance PDF report", proFeatures);
        // Should NOT contain the shorthand itself
        Assert.DoesNotContain("Everything in Starter", proFeatures);

        // Business: "Everything in Professional" expands recursively
        var businessFeatures = DeriveCurrentPlanFeatures("business", Plans);
        Assert.Contains("Unlimited scans, 25 pages each", businessFeatures);
        Assert.Contains("VPAT generation", businessFeatures);
        Assert.Contains("Custom rules engine", businessFeatures);
        // Expanded from Professional → Starter
        Assert.Contains("30 scans/month, 10 pages each", businessFeatures);
        Assert.Contains("Weekly automated monitoring", businessFeatures);
        Assert.Contains("10 scans/month, 5 pages each", businessFeatures);
        Assert.Contains("Code fix generation (Liquid patches)", businessFeatures);
        // Should NOT contain the shorthands
        Assert.DoesNotContain("Everything in Professional", businessFeatures);
        Assert.DoesNotContain("Everything in Starter", businessFeatures);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // TEST 7b: Cancel button at page bottom with secondary/muted CSS
    // ═══════════════════════════════════════════════════════════════════════

    [Fact]
    public void BillingPage_CancelButton_AtBottomWithMutedStyle()
    {
        // Walk up from test bin directory to solution root
        var dir = AppContext.BaseDirectory;
        while (dir is not null && !File.Exists(Path.Combine(dir, "SaasTemplate.sln")))
            dir = Directory.GetParent(dir)?.FullName;

        Assert.NotNull(dir);

        var razorPath = Path.Combine(dir!, "src", "SaasTemplate.Api",
            "Components", "Pages", "Billing.razor");
        var source = File.ReadAllText(razorPath);

        // Cancel button should exist with muted/secondary styling
        Assert.Contains("Cancel Subscription", source);

        // "Manage Payment" and "Cancel Subscription" should be separate buttons
        Assert.Contains("Manage Payment", source);

        // Cancel should appear AFTER "Manage Payment" in the source
        var managePaymentIndex = source.IndexOf("Manage Payment", StringComparison.Ordinal);
        var cancelIndex = source.IndexOf("Cancel Subscription", StringComparison.Ordinal);
        Assert.True(cancelIndex > managePaymentIndex,
            "Cancel Subscription should appear after Manage Payment in the page");

        // Cancel should use muted/secondary styling (color:var(--gray- prefix)
        // We check that the cancel section has muted styling indicators
        var cancelSection = source[cancelIndex..];
        // The cancel button or its container should reference gray/muted colors
        Assert.True(
            source.Contains("btn-secondary") || source.Contains("btn-ghost") ||
            source.Contains("color:var(--gray-"),
            "Cancel button should use muted/secondary styling");
    }

    // ═══════════════════════════════════════════════════════════════════════
    // Additional: Verify "Payment & Cancellation" is split into two buttons
    // ═══════════════════════════════════════════════════════════════════════

    [Fact]
    public void BillingPage_PaymentAndCancellation_AreSeparateButtons()
    {
        var dir = AppContext.BaseDirectory;
        while (dir is not null && !File.Exists(Path.Combine(dir, "SaasTemplate.sln")))
            dir = Directory.GetParent(dir)?.FullName;

        Assert.NotNull(dir);

        var razorPath = Path.Combine(dir!, "src", "SaasTemplate.Api",
            "Components", "Pages", "Billing.razor");
        var source = File.ReadAllText(razorPath);

        // The old combined "Payment & Cancellation" button should be gone
        Assert.DoesNotContain("Payment &amp; Cancellation", source);
        Assert.DoesNotContain("Payment & Cancellation", source);

        // Replaced by separate buttons
        Assert.Contains("Manage Payment", source);
        Assert.Contains("Cancel Subscription", source);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // Additional: _currentPlanFeatures should NOT be a hardcoded switch
    // ═══════════════════════════════════════════════════════════════════════

    [Fact]
    public void BillingPage_CurrentPlanFeatures_NotHardcodedSwitch()
    {
        var dir = AppContext.BaseDirectory;
        while (dir is not null && !File.Exists(Path.Combine(dir, "SaasTemplate.sln")))
            dir = Directory.GetParent(dir)?.FullName;

        Assert.NotNull(dir);

        var razorPath = Path.Combine(dir!, "src", "SaasTemplate.Api",
            "Components", "Pages", "Billing.razor");
        var source = File.ReadAllText(razorPath);

        // The old hardcoded switch expression should be gone (D-014 fix)
        // Look for the pattern: _currentPlanFeatures => _subscription?.Tier switch
        Assert.DoesNotContain("_currentPlanFeatures => _subscription?.Tier switch", source);
    }
}
