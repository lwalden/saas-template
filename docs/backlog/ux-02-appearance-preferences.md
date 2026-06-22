# UX-02: Appearance & accessibility preferences

- **Track:** UX · **Priority:** P1 · **Effort:** M · **Depends on:** UX-01 · **Status:** Backlog

## Problem / Why
Modern SaaS lets users tune appearance and accessibility. Today `Settings.razor`
only has a marketing-consent toggle. We need a persisted "Appearance" section so
preferences survive across devices/sessions, not just localStorage.

## Current state (in this repo)
- `Components/Pages/Settings.razor` — email preferences only; good ARIA section
  structure to mirror.
- `Data/Entities.cs` `ApplicationUser` — no UI/preference columns.
- UX-01 provides the theming engine and a temporary toggle to replace here.

## Acceptance criteria
- [ ] New "Appearance" section in Settings with: theme (System/Light/Dark),
      UI density (Comfortable/Compact), text size scale, and a "Reduce motion"
      override (in addition to the OS setting).
- [ ] Preferences persist on `ApplicationUser` (or a `UserPreferences` entity) via
      an EF migration; applied on login so they follow the user across devices.
- [ ] Density and text-size map to CSS custom properties (spacing/`font-size` roots)
      so they compose with the UX-01 token system.
- [ ] Controls are keyboard-operable, labelled, and announce changes (`aria-live`).
- [ ] Server preference takes precedence over the pre-paint localStorage guess once loaded.

## Implementation notes
- Add columns (`Theme`, `UiDensity`, `TextScale`, `ReduceMotion`) and a migration
  per `CLAUDE.md` conventions; back them with a small endpoint or Blazor handler.
- Reduced-motion override should set a `data-motion="reduce"` attribute consumed
  by the motion rules consolidated in UX-06.

## Out of scope
- Color-blind/high-contrast palettes (UX-06), per-workspace branding.
