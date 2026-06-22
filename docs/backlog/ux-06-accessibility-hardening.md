# UX-06: WCAG 2.2 AA hardening + automated a11y CI gate

- **Track:** UX · **Priority:** P1 · **Effort:** M · **Depends on:** — · **Status:** Backlog

## Problem / Why
The template has a good WCAG 2.1 AA foundation but several gaps and no automated
guardrail. 2026 baseline is **WCAG 2.2 AA**, and accessibility regressions should
be caught in CI, not review.

## Current state (in this repo)
- Strengths: skip links, ARIA on nav/dialogs, focus traps in `App.razor`.
- Gaps found in audit: `prefers-reduced-motion` not applied to several transitions
  (button/nav/sidebar/dialog-backdrop in `site.css`/`input.css`); no roving tabindex
  on menus; no required-field indicators; some status conveyed by color only
  (Billing badges); inconsistent `:focus-visible` on dialog buttons; horizontal
  scroll on tables instead of reflow (WCAG 1.4.10).
- No automated accessibility testing anywhere in `tests/` or `.github/workflows/`.

## Acceptance criteria
- [ ] All non-essential motion (including hover/transition/drawer/backdrop) is gated
      behind `prefers-reduced-motion` and the UX-02 `data-motion="reduce"` override.
- [ ] Menus/dropdowns implement roving tabindex; visible `:focus-visible` rings on all
      interactive controls including dialog buttons.
- [ ] Required form fields are programmatically and visually indicated.
- [ ] No information conveyed by color alone (add icon/text to status badges).
- [ ] Data tables reflow (no horizontal scroll) at mobile breakpoints, or expose an
      accessible alternative.
- [ ] WCAG 2.2-specific checks reviewed (target size 2.5.8, focus-not-obscured 2.4.11,
      consistent help 3.2.6, accessible authentication 3.3.8 — magic link already helps).
- [ ] Automated a11y check runs in CI (e.g. Playwright + axe-core, or `bUnit` render
      assertions) and fails the build on new violations; wired into `.github/workflows/ci.yml`.

## Implementation notes
- Centralize motion rules so the reduced-motion override is one place, not per rule.
- If adding Playwright+axe, keep it a separate test category so the existing
  `Category!=SqlServer&Category!=Smoke` filter still runs fast locally.

## Out of scope
- Full localization/RTL (UX-08); color-blind palette options (UX-02).
