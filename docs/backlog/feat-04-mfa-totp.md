# FEAT-04: MFA / TOTP two-factor auth

- **Track:** Feature · **Priority:** P1 · **Effort:** M · **Depends on:** FEAT-03 · **Status:** Backlog

## Problem / Why
The `TwoFactorEnabled` column exists in the Identity schema but there is no MFA
implementation. Authenticator-app (TOTP) 2FA with recovery codes is expected for
any product handling real accounts/billing.

## Current state (in this repo)
- `Data/Entities.cs` `ApplicationUser` inherits `TwoFactorEnabled` (present, **disabled/unused**).
- `Auth/AuthEndpoints.cs` login issues JWT directly with no second-factor step.

## Acceptance criteria
- [ ] Users can enroll an authenticator app (TOTP): show QR + manual key, verify a code
      to enable, using Identity's authenticator token provider.
- [ ] One-time recovery codes generated on enable, shown once, regenerable.
- [ ] Login requires the second factor when enabled (interstitial step before JWT issuance),
      with a "remember this device" option (configurable trust window).
- [ ] Disable-MFA requires re-authentication; security-relevant changes are audited (FEAT-09).
- [ ] MFA section in Settings (status, enable/disable, regenerate codes).
- [ ] Tests cover enroll/verify, login-with-TOTP, recovery-code login, and disable.

## Implementation notes
- Use the built-in ASP.NET Core Identity authenticator support; don't roll your own TOTP.
- Decide how MFA interacts with magic-link/OAuth logins and document it.

## Out of scope
- SMS/WebAuthn/passkeys (passkeys are a strong future story), org-enforced MFA policy.
