# UX-07: Form UX system (validation summary, async, help)

- **Track:** UX · **Priority:** P2 · **Effort:** M · **Depends on:** UX-03 · **Status:** Backlog

## Problem / Why
Forms are hand-rolled per page with manual error-string state, no error summary,
no field-level help, no async validation, and placeholders doubling as labels.
A small form system makes new forms consistent, accessible, and quick to build.

## Current state (in this repo)
- `Components/Pages/Login.razor` and `Billing.razor` manage `_error`/`_emailError`
  strings by hand; no form-level error summary; no async (e.g. email-uniqueness) check.
- Placeholder used as label in `Login.razor` (`you@example.com`).

## Acceptance criteria
- [ ] A `Field` wrapper (label, required marker, help text via `aria-describedby`,
      inline error with `aria-invalid`) built on the UX-03 `Input`.
- [ ] An optional form-level **error summary** that lists errors and focuses the first
      invalid field on submit.
- [ ] Support for async/server validation states (pending/valid/invalid) with
      `aria-busy` and debounce; one real example (e.g. email availability).
- [ ] Help/hint text pattern distinct from error text; placeholders never replace labels.
- [ ] Login/Billing forms migrated to the new system with no behavior regression.

## Implementation notes
- Prefer Blazor `EditForm` + `DataAnnotations`/`FluentValidation` so validation is
  declarative; keep the magic-link/Stripe submit flows intact.

## Out of scope
- Multi-step/wizard forms (could be a later story if onboarding grows).
