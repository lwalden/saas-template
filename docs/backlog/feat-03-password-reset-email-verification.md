# FEAT-03: Password reset & email verification

- **Track:** Feature · **Priority:** P0 · **Effort:** M · **Depends on:** — · **Status:** Backlog

## Problem / Why
Two table-stakes auth flows are missing. Password/email accounts can be created but
there is **no password-reset flow** and **no email-verification/confirmation** before
use. These are baseline security/UX expectations and a common audit finding.

## Current state (in this repo)
- `Auth/AuthEndpoints.cs` — registration/login, magic link, Google OAuth; OAuth sets
  `EmailConfirmed` automatically but email/password registration does not verify email.
- `Email/ResendEmailService.cs` already sends magic-link/dunning/onboarding mail (reuse).
- Identity provides `GeneratePasswordResetTokenAsync` / `GenerateEmailConfirmationTokenAsync`.

## Acceptance criteria
- [ ] Forgot-password: request endpoint + page emails a tokenized reset link (Identity
      reset token), reset page sets a new password; tokens are single-use and expiring.
- [ ] Email verification: on email/password signup, send a confirmation link; unverified
      accounts are clearly flagged and (configurably) gated from sensitive actions.
- [ ] Resend-verification and resend-reset are rate-limited (reuse the sliding-window
      limiter policies in `Program.cs`); responses don't leak whether an email exists.
- [ ] Confirmation/reset emails follow the existing CAN-SPAM footer pattern.
- [ ] Tests cover happy path, expired/used token, and unknown-email non-enumeration.

## Implementation notes
- Mirror the magic-link handler structure already in `AuthEndpoints.cs`.
- Keep messaging identical for existing vs. non-existing emails to prevent enumeration.

## Out of scope
- MFA (FEAT-04), additional OAuth providers (FEAT-05).
