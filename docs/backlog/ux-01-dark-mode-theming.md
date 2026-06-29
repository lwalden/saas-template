# UX-01: Dark mode & theming foundation

- **Track:** UX · **Priority:** P0 · **Effort:** L · **Depends on:** — · **Status:** ✅ Done (Sprint 2)

> **Shipped:** semantic CSS-custom-property layer on `:root` flipped under
> `:root[data-theme="dark"]`, WCAG-2.1-AA dark palette, render-blocking pre-paint
> bootstrap in `App.razor` (no FOUC), explicit→system resolution, reduced-motion
> guard, and a temporary System/Light/Dark toggle in Settings. Tokens are authored
> in the hand-maintained served `wwwroot/css/app.css` and mirrored into `input.css`'s
> `@theme` (see the note in those files explaining why `build:css` was not repointed).
> **Deferred:** the polished toggle UI and server-side persistence belong to UX-02.

## Problem / Why
A dark theme is a baseline expectation in 2026. The template ships light-only.
Marketing/auth pages use hard-coded dark gradients while the app shell is
light-only, so there is no single source of truth for theme. We need a real
theming layer (system / light / dark) driven by CSS custom properties, with the
user's choice persisted and applied before first paint (no flash of wrong theme).

## Current state (in this repo)
- `input.css` `@theme` block defines a strong but **light-only** token palette
  (surfaces `#FCFAFF…#E2E0FC`, text `#1A1A2E`, etc.). Orphan utilities
  `.input-dark` / `.btn-secondary-dark` exist but are never wired to a theme.
- No `prefers-color-scheme` handling in component logic; no toggle anywhere.
- `Components/App.razor` already runs early inline JS (version check, timezone) —
  a good hook point for a pre-paint theme bootstrap.
- `Components/Pages/Settings.razor` currently exposes only email preferences.

## Acceptance criteria
- [ ] Color tokens are expressed as CSS custom properties on `:root` (light) and
      overridden under `:root[data-theme="dark"]`; `input.css` utilities reference
      the variables, not raw hex, so both themes share one component layer.
- [ ] A dark palette meets WCAG 2.1 AA contrast (≥4.5:1 body, ≥3:1 large/UI).
- [ ] Theme resolves from: explicit user choice → else system (`prefers-color-scheme`).
- [ ] An inline, render-blocking script in `App.razor` sets `data-theme` on `<html>`
      before first paint to prevent a flash of incorrect theme.
- [ ] Choice persists (localStorage + the server-side preference from UX-02 when present).
- [ ] Existing dark marketing/auth sections are refactored to the token variables.
- [ ] `prefers-reduced-motion` users get no theme-transition animation.

## Implementation notes
- Keep the Tailwind v4 `@theme` for the scale; map semantic tokens
  (`--surface`, `--surface-raised`, `--text`, `--text-muted`, `--border`,
  `--brand`, semantic states) to it and flip them per `data-theme`.
- Re-run the `input.css → style.css` build (`package.json` scripts) after token changes.
- The toggle UI lives in UX-02; this story delivers the engine + a temporary toggle.

## Out of scope
- Per-workspace brand theming (future), high-contrast mode (UX-02/UX-06).
