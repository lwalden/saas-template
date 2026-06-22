# UX-03: Reusable component library & documented design tokens

- **Track:** UX · **Priority:** P1 · **Effort:** L · **Depends on:** — · **Status:** Backlog

## Problem / Why
The design system lives entirely in generated CSS, and markup is duplicated
across pages (breadcrumbs, dialogs, badges, buttons hand-rolled per page). There
is no documented type scale or spacing scale, and radius values are inconsistent
(4px vs 8px). 2026 teams expect a small, documented Blazor component layer plus a
living style guide so screens compose instead of copy-paste.

## Current state (in this repo)
- `input.css` defines colors but **no documented type or spacing scale**; font
  sizes are inline (`font-size:0.875rem`, etc.).
- Dialogs are inline-styled (`Components/Pages/Billing.razor` ~60% inline styles);
  breadcrumbs are hard-coded in `Components/Layout/MainLayout.razor`.
- Button/badge/input styles exist as CSS classes but have no Razor wrappers.

## Acceptance criteria
- [ ] Documented, tokenized **type scale** and **spacing scale** added to the
      `@theme` layer; one canonical radius scale; usages migrated off inline sizes.
- [ ] Reusable Razor components for the core primitives: `Button`, `Input`/`Field`,
      `Badge`, `Card`, `Dialog`/`Modal`, `Breadcrumbs`, `Avatar`, `Dropdown`.
      Existing pages refactored to use them (no behavior/visual regression).
- [ ] A `/styleguide` page (dev/internal) renders every component + token in light
      and dark (UX-01), serving as living documentation.
- [ ] Components are theme-aware (consume UX-01 variables) and accessible by default
      (labels, focus-visible, ARIA wired in the component, not per call site).

## Implementation notes
- Put components under `Components/Ui/`; keep them presentational/parameterized.
- Migrate `Billing.razor` and `Pricing.razor` dialogs to the shared `Dialog`
  component (reuse the `trapFocus`/`releaseFocus` helpers already in `App.razor`).
- Gate `/styleguide` behind a non-production check or `[Authorize]` admin policy.

## Out of scope
- Figma sync / Code Connect (could follow once components stabilize).
