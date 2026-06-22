# FEAT-05: Session management, more OAuth, account deletion/GDPR export

- **Track:** Feature · **Priority:** P1 · **Effort:** L · **Depends on:** — · **Status:** Backlog

## Problem / Why
Account-security hygiene is thin: JWTs can't be revoked, Google is the only social
provider, and there's no self-serve account deletion or data export. GDPR/CCPA make
deletion + export effectively mandatory for consumer SaaS.

## Current state (in this repo)
- `Auth/TokenService.cs` issues stateless HS256 JWTs (60-min) with **no revocation/blocklist**;
  `Auth/DashboardSession.cs` holds the JWT per Blazor circuit.
- `Program.cs` configures **Google OAuth only**.
- No account-deletion or data-export path; deletes are hard deletes.

## Acceptance criteria
- [ ] Active-sessions view: list current sessions/devices with last-seen, allow
      "sign out" individually and "sign out everywhere"; revocation actually invalidates
      tokens (short-lived JWT + refresh/session store, or a token-version claim checked on use).
- [ ] At least one additional OAuth provider wired (e.g. GitHub or Microsoft) following the
      existing Google prerender cookie-handoff pattern; account linking for same-email logins.
- [ ] Self-serve account deletion with confirmation + grace period; cancels Stripe
      subscription and removes/anonymizes PII (coordinate with FEAT-16 soft-delete if present).
- [ ] GDPR data export: user can request a machine-readable export of their personal data.
- [ ] Security-relevant events recorded (FEAT-09) and a confirmation email sent.
- [ ] Tests for revocation, OAuth link, deletion side-effects, and export contents.

## Implementation notes
- A `token_version`/`security_stamp` claim compared on each request is the lightest
  revocation mechanism that fits the current stateless design.

## Out of scope
- SAML/enterprise SSO, SCIM deprovisioning.
