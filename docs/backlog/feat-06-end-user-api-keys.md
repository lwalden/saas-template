# FEAT-06: End-user API keys & developer access

- **Track:** Feature · **Priority:** P1 · **Effort:** M · **Depends on:** — · **Status:** Backlog

## Problem / Why
There's a JWT bearer API but no way for customers to get programmatic credentials.
Self-serve API keys are a common SaaS expectation and enable integrations without
sharing login credentials.

## Current state (in this repo)
- `Auth/TokenService.cs` + JWT bearer auth exist for first-party calls.
- `Monitoring/OpsEndpoints.cs` shows an internal `X-Api-Key` pattern (constant-time
  compare) — a good reference, but that's an **admin** key, not per-user.

## Acceptance criteria
- [ ] `ApiKey` entity (owner = user or org per FEAT-01, name, hashed secret, prefix,
      scopes, last-used, created, revoked) with EF migration.
- [ ] Keys are shown in full **once** at creation, then only by prefix; stored as a hash,
      never plaintext.
- [ ] An authentication scheme accepts API keys (e.g. `Authorization: Bearer sk_...` or a
      header) and resolves the owning principal + scopes; rate-limited like other auth.
- [ ] Settings UI to create, name, scope, list, and revoke keys; last-used timestamp updates.
- [ ] Key actions are audited (FEAT-09); creating/revoking is permission-gated (FEAT-02).
- [ ] Tests for auth via key, scope enforcement, and revocation.

## Implementation notes
- Hash with a strong KDF; use a random prefix for display/lookup. Reuse the constant-time
  comparison approach from `OpsEndpoints.cs`.
- Consider scoping keys to the current organization once FEAT-01 lands.

## Out of scope
- OAuth client-credentials / third-party app authorization (a larger platform story).
