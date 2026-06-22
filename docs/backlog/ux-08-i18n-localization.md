# UX-08: Internationalization & localization readiness

- **Track:** UX · **Priority:** P2 · **Effort:** L · **Depends on:** — · **Status:** Backlog

## Problem / Why
Every user-facing string is hard-coded English (UI chrome, page copy, plan/feature
lists, C# error messages, transactional emails). Retrofitting i18n later is costly;
establishing the framework now keeps the template ready for global SaaS.

## Current state (in this repo)
- No `IStringLocalizer`, resource files, locale switcher, or RTL support.
- Hard-coded strings across `Components/**`, `Billing/**`, `Email/**`.
- Dates formatted with fixed patterns (`Billing.razor` `ToString("MMMM d, yyyy")`),
  not locale/culture-aware.

## Acceptance criteria
- [ ] Localization wired up (`AddLocalization`, `RequestLocalizationMiddleware`) with a
      default culture and a `SupportedCultures` list.
- [ ] UI strings moved to `IStringLocalizer` + `.resx` resources; at least the app shell,
      Login, Pricing, Billing, Settings externalized, plus a second sample locale.
- [ ] A locale selector in Settings (persisted per UX-02 preferences), with cookie fallback.
- [ ] Dates/numbers/currency formatted culture-aware.
- [ ] RTL readiness: layout uses logical CSS properties / `dir` attribute so an RTL
      locale renders correctly; verify the shell doesn't break.
- [ ] Transactional emails (`Email/ResendEmailService.cs`) accept a culture and select
      localized templates.

## Implementation notes
- Keep resource keys stable; provide a machine-translatable second locale as a smoke test.
- Coordinate with FEAT-08 (plan/feature copy) so pricing text is localizable too.

## Out of scope
- Professional translation/TMS integration; locale-specific legal pages.
