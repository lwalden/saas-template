using Xunit;

namespace SaasTemplate.Api.Tests.Billing;

/// <summary>
/// Verifies that the billing page Razor markup contains proration messaging
/// for Starter-tier subscribers who see the upgrade prompt.
/// </summary>
public class BillingProrationTests
{
    private static readonly string _razorPath = Path.Combine(
        AppContext.BaseDirectory, "..", "..", "..", "..", "..",
        "src", "SaasTemplate.Api", "Components", "Pages", "Billing.razor");

    [Fact]
    public void BillingRazor_contains_proration_note()
    {
        var content = File.ReadAllText(_razorPath);
        Assert.Contains("prorated", content, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void BillingRazor_proration_note_is_inside_change_plan_section()
    {
        var content = File.ReadAllText(_razorPath);
        // S31-003: upgrade section replaced by per-tier change plan sections (e.g. change-starter-heading)
        var changeIndex = content.IndexOf("change-", StringComparison.Ordinal);
        var prorationIndex = content.IndexOf("prorated", StringComparison.OrdinalIgnoreCase);

        Assert.True(changeIndex > 0, "change plan section not found");
        Assert.True(prorationIndex > 0, "proration text not found");
        Assert.True(prorationIndex > changeIndex, "proration note should appear after change plan heading");
    }
}
