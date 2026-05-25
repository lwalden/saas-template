using System.Text.RegularExpressions;
using Xunit;

namespace SaasTemplate.Api.Tests.Content;

/// <summary>
/// Source-level tests for static HTML pages and CSS — catches markup/style issues
/// that don't require a running server to verify.
/// </summary>
public class StaticPageTests
{
    private static string SolutionRoot()
    {
        var dir = AppContext.BaseDirectory;
        while (dir is not null && !File.Exists(Path.Combine(dir, "SaasTemplate.sln")))
            dir = Directory.GetParent(dir)?.FullName;
        Assert.NotNull(dir);
        return dir!;
    }

    // ── Static page footer ────────────────────────────────────────────────

    [Theory]
    [InlineData("privacy.html")]
    [InlineData("terms.html")]
    public void Static_page_has_full_footer_with_nav_links(string relativePath)
    {
        var path = Path.Combine(SolutionRoot(), relativePath.Replace('/', Path.DirectorySeparatorChar));
        var source = File.ReadAllText(path);

        Assert.Contains("Privacy Policy", source);
        Assert.Contains("Terms of Service", source);
    }

    // ── Design token constraints ──────────────────────────────────────────

    [Fact]
    public void MutedToken_in_inputCss_is_not_727784()
    {
        // #727784 on tinted lavender backgrounds (e.g. #f5f2ff) gives 4.05:1 —
        // below the WCAG 2.1 AA threshold of 4.5:1. The --color-muted token
        // must be a darker value (other tokens may legitimately reuse the hex).
        var inputCssPath = Path.Combine(SolutionRoot(), "input.css");
        var css = File.ReadAllText(inputCssPath);

        Assert.DoesNotContain("--color-muted:             #727784", css);
    }

    [Fact]
    public void MutedToken_in_appCss_is_not_727784()
    {
        // Verify the compiled app.css override also uses the corrected muted color.
        var appCssPath = Path.Combine(SolutionRoot(), "src", "SaasTemplate.Api",
            "wwwroot", "css", "app.css");
        var css = File.ReadAllText(appCssPath);

        Assert.DoesNotContain("--color-muted: #727784", css);
    }

    [Fact]
    public void CompiledStyleCss_contains_updated_muted_color()
    {
        // Confirms that the Tailwind build ran after updating input.css and the
        // corrected token value (#595f6e) propagated into the compiled style.css.
        var styleCssPath = Path.Combine(SolutionRoot(), "style.css");
        var css = File.ReadAllText(styleCssPath);

        Assert.Contains("#595f6e", css);
    }

    [Fact]
    public void AccentToken_in_inputCss_is_not_0077B6()
    {
        // #0077B6 on tinted surfaces gives 4.41:1 — below WCAG 2.1 AA (4.5:1).
        // The --color-accent token must be darkened to pass on tinted surfaces.
        var inputCssPath = Path.Combine(SolutionRoot(), "input.css");
        var css = File.ReadAllText(inputCssPath);

        Assert.DoesNotContain("--color-accent:            #0077B6", css);
    }

    [Fact]
    public void AccentToken_in_inputCss_uses_006FA6()
    {
        // #006FA6 gives 4.97:1 on #f5f2ff and 5.49:1 on white — passes AA on both.
        var inputCssPath = Path.Combine(SolutionRoot(), "input.css");
        var css = File.ReadAllText(inputCssPath);

        Assert.Contains("--color-accent:            #006FA6", css);
    }

    [Fact]
    public void CompiledStyleCss_contains_updated_accent_color()
    {
        // Confirms the Tailwind build ran after updating input.css and the
        // corrected accent value (#006fa6) propagated into the compiled style.css.
        var styleCssPath = Path.Combine(SolutionRoot(), "style.css");
        var css = File.ReadAllText(styleCssPath);

        Assert.Contains("006fa6", css, StringComparison.OrdinalIgnoreCase);
    }
}
