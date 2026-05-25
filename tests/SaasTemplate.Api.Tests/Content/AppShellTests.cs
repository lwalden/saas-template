using Xunit;

namespace SaasTemplate.Api.Tests.Content;

/// <summary>
/// Validates the app shell redesign: sidebar nav refinements, mobile drawer,
/// top bar with breadcrumb, profile dropdown, and design-system token alignment.
/// S34-005
/// </summary>
public class AppShellTests
{
    private static readonly string AppCss = File.ReadAllText(
        Path.Combine(TestHelpers.FindRepoRoot(), "src", "SaasTemplate.Api", "wwwroot", "css", "app.css"));

    private static readonly string MainLayoutRazor = File.ReadAllText(
        Path.Combine(TestHelpers.FindRepoRoot(), "src", "SaasTemplate.Api", "Components", "Layout", "MainLayout.razor"));

    // ── Design System Token Alignment ──

    [Theory]
    [InlineData("--color-surface: #FCFAFF")]
    [InlineData("--color-surface-low: #F5F2FF")]
    [InlineData("--color-surface-lowest: #FFFFFF")]
    [InlineData("--color-charcoal: #1A1A2E")]
    [InlineData("--color-secondary: #414753")]
    [InlineData("--color-muted: #595f6e")]
    [InlineData("--navy-mid: #0A2540")]
    public void Css_contains_design_system_tokens(string token)
    {
        Assert.Contains(token, AppCss);
    }

    [Fact]
    public void Body_uses_charcoal_token_for_color()
    {
        Assert.Contains("color: var(--color-charcoal)", AppCss);
    }

    [Fact]
    public void Body_uses_surface_token_for_background()
    {
        Assert.Contains("background: var(--color-surface)", AppCss);
    }

    // ── Top Bar ──

    [Fact]
    public void Css_contains_top_bar_class()
    {
        Assert.Contains(".top-bar", AppCss);
    }

    [Fact]
    public void Top_bar_has_fixed_position()
    {
        Assert.Contains("position: fixed", AppCss);
    }

    [Fact]
    public void Top_bar_offset_by_sidebar_width()
    {
        Assert.Contains("left: var(--sidebar-width)", AppCss);
    }

    // ── Breadcrumb ──

    [Fact]
    public void Css_contains_breadcrumb_classes()
    {
        Assert.Contains(".breadcrumb", AppCss);
        Assert.Contains(".breadcrumb-current", AppCss);
    }

    // ── Profile Dropdown ──

    [Fact]
    public void Css_contains_profile_trigger()
    {
        Assert.Contains(".profile-trigger", AppCss);
    }

    [Fact]
    public void Css_contains_profile_menu()
    {
        Assert.Contains(".profile-menu", AppCss);
    }

    [Fact]
    public void Profile_trigger_has_focus_visible()
    {
        Assert.Contains(".profile-trigger:focus-visible", AppCss);
    }

    [Fact]
    public void Profile_avatar_class_exists()
    {
        Assert.Contains(".profile-avatar", AppCss);
    }

    // ── Mobile Drawer ──

    [Fact]
    public void Css_contains_mobile_header()
    {
        Assert.Contains(".mobile-header", AppCss);
    }

    [Fact]
    public void Css_contains_hamburger_button()
    {
        Assert.Contains(".hamburger-btn", AppCss);
    }

    [Fact]
    public void Css_contains_sidebar_backdrop()
    {
        Assert.Contains(".sidebar-backdrop", AppCss);
    }

    [Fact]
    public void Mobile_media_query_hides_top_bar()
    {
        // At 768px, top-bar should be hidden
        Assert.Contains(".top-bar { display: none; }", AppCss);
    }

    [Fact]
    public void Mobile_sidebar_uses_transform()
    {
        Assert.Contains("transform: translateX(-100%)", AppCss);
        Assert.Contains("transform: translateX(0)", AppCss);
    }

    // ── Sidebar Refinements ──

    [Fact]
    public void Sidebar_uses_gradient_background()
    {
        Assert.Contains("linear-gradient(180deg, #1A1A40 0%, #0D0D20 100%)", AppCss);
    }

    [Fact]
    public void Nav_links_have_border_left()
    {
        Assert.Contains("border-left: 3px solid transparent", AppCss);
    }

    [Fact]
    public void Sidebar_user_class_exists()
    {
        Assert.Contains(".sidebar-user", AppCss);
        Assert.Contains(".sidebar-user-email", AppCss);
    }

    // ── Reduced Motion ──

    [Fact]
    public void Reduced_motion_covers_sidebar_transitions()
    {
        Assert.Contains("@media (prefers-reduced-motion: reduce)", AppCss);
    }

    // ── MainLayout Markup ──

    [Fact]
    public void Layout_has_skip_link()
    {
        Assert.Contains("skip-link", MainLayoutRazor);
        Assert.Contains("Skip to main content", MainLayoutRazor);
    }

    [Fact]
    public void Layout_has_mobile_header()
    {
        Assert.Contains("mobile-header", MainLayoutRazor);
    }

    [Fact]
    public void Layout_has_hamburger_with_aria()
    {
        Assert.Contains("hamburger-btn", MainLayoutRazor);
        Assert.Contains("aria-expanded", MainLayoutRazor);
        Assert.Contains("aria-controls", MainLayoutRazor);
    }

    [Fact]
    public void Layout_has_sidebar_backdrop()
    {
        Assert.Contains("sidebar-backdrop", MainLayoutRazor);
    }

    [Fact]
    public void Layout_has_sidebar_with_aria_label()
    {
        Assert.Contains(@"aria-label=""Application navigation""", MainLayoutRazor);
    }

    [Fact]
    public void Layout_has_top_bar_with_breadcrumb()
    {
        Assert.Contains("top-bar", MainLayoutRazor);
        Assert.Contains("breadcrumb", MainLayoutRazor);
    }

    [Fact]
    public void Layout_has_profile_dropdown()
    {
        Assert.Contains("profile-trigger", MainLayoutRazor);
        Assert.Contains("profile-menu", MainLayoutRazor);
        Assert.Contains("profile-avatar", MainLayoutRazor);
    }

    [Fact]
    public void Profile_dropdown_has_aria_haspopup()
    {
        Assert.Contains(@"aria-haspopup=""true""", MainLayoutRazor);
    }

    [Fact]
    public void Layout_has_sidebar_user_section()
    {
        Assert.Contains("sidebar-user", MainLayoutRazor);
        Assert.Contains("sidebar-user-email", MainLayoutRazor);
    }

    [Fact]
    public void Layout_has_nav_active_aria_current()
    {
        // All nav links should use aria-current="page" for active state
        Assert.Contains(@"aria-current=""@(IsActivePage", MainLayoutRazor);
    }

    [Fact]
    public void Layout_code_has_sidebar_toggle_fields()
    {
        Assert.Contains("_sidebarOpen", MainLayoutRazor);
        Assert.Contains("_dropdownOpen", MainLayoutRazor);
        Assert.Contains("_currentPageName", MainLayoutRazor);
    }

    [Fact]
    public void Layout_code_has_toggle_methods()
    {
        Assert.Contains("ToggleSidebar", MainLayoutRazor);
        Assert.Contains("CloseSidebar", MainLayoutRazor);
        Assert.Contains("ToggleDropdown", MainLayoutRazor);
    }

    [Fact]
    public void Layout_code_has_escape_key_handler()
    {
        Assert.Contains("OnDropdownKeyDown", MainLayoutRazor);
        Assert.Contains("Escape", MainLayoutRazor);
    }

    [Fact]
    public void Layout_code_has_get_page_name()
    {
        Assert.Contains("GetPageName", MainLayoutRazor);
    }

    [Fact]
    public void Main_content_accounts_for_top_bar_height()
    {
        Assert.Contains("padding-top: calc(48px + 2rem)", AppCss);
    }

    private static class TestHelpers
    {
        public static string FindRepoRoot()
        {
            var dir = Directory.GetCurrentDirectory();
            while (dir is not null && !Directory.Exists(Path.Combine(dir, ".git")))
                dir = Directory.GetParent(dir)?.FullName;
            return dir ?? throw new InvalidOperationException("Could not find repo root");
        }
    }
}
