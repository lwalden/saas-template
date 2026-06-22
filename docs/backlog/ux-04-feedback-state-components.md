# UX-04: Toasts, skeleton loaders & empty states

- **Track:** UX · **Priority:** P1 · **Effort:** M · **Depends on:** UX-03 · **Status:** Backlog

## Problem / Why
Feedback patterns are missing. Success/error is shown only as inline text;
loading is a literal "Loading…" string; there is no reusable empty state. These
three patterns (transient toasts, skeletons, empty states) are 2026 table stakes
and remove most ad-hoc status handling from pages.

## Current state (in this repo)
- `Components/Pages/Home.razor` uses a `.loading` text placeholder.
- Errors/success are inline `role="alert"` / `role="status"` strings repeated per page.
- No empty-state treatment (the dashboard "Getting Started" card is bespoke).

## Acceptance criteria
- [ ] A toast service + host: `ToastService.Show(message, severity)` rendered once
      in `MainLayout`, with success/info/warning/error variants, auto-dismiss,
      manual close, and an `aria-live="polite"`/`role="status"` region (errors `assertive`).
- [ ] A `Skeleton` component (text/line/card/table-row variants) replacing the
      "Loading…" text on at least `Home.razor` and `Billing.razor` initial loads.
- [ ] An `EmptyState` component (icon, title, description, optional CTA) used for at
      least one real empty case (e.g. no subscription / no data).
- [ ] Toasts and skeletons respect reduced motion; toasts don't trap focus and are
      dismissible via keyboard.

## Implementation notes
- Build on UX-03 primitives and UX-01 theme variables.
- Migrate existing inline alerts in `Login.razor` / `Billing.razor` to toasts where
  a transient message is appropriate; keep inline field errors for form validation.

## Out of scope
- In-app **persistent** notification center — that's FEAT-10 (different concern).
