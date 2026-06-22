# FEAT-09: Audit log & activity trail

- **Track:** Feature · **Priority:** P1 · **Effort:** M · **Depends on:** — · **Status:** Done (foundation; queue + tenant view deferred)

> **Shipped:** `AuditEvent` entity + EF migration + indexes; `IAuditLogger`/`AuditLogger`
> (resilient — a write failure is logged, never thrown into the caller; captures IP/user-agent
> from the HTTP context); auth flows emit events (register, login success/failure, password-reset
> requested/completed, email-verification sent/verified); queryable via `GET /api/ops/audit`
> (X-Api-Key guarded, filter by user/action, capped at 500). Covered by `AuditLogTests`.
> **Deferred:** moving writes onto the background-job queue (FEAT-13), tenant-scoped admin UI
> (needs FEAT-01 + FEAT-12), and retention/pruning.

## Problem / Why
There is no record of who did what, when. Audit trails are needed for security
investigations, compliance (SOC 2 / GDPR), and customer-facing activity views, and
several other stories (auth changes, RBAC, API keys, billing) should write to it.

## Current state (in this repo)
- No audit entity or service anywhere in `Data/` or services.
- `Webhooks/WebhookDispatcher.cs` emits some outbound events (signup/subscription/payment)
  but that's integration egress, not a queryable internal audit log.

## Acceptance criteria
- [ ] `AuditEvent` entity (timestamp, actor user/org, action, target, IP/user-agent,
      metadata JSON) with an EF migration and appropriate indexes.
- [ ] An `IAuditLogger` abstraction that's cheap to call from anywhere; writes are
      non-blocking and never break the originating request.
- [ ] Security/billing/account-management actions emit audit events (login, MFA change,
      password reset, role change, invite, API-key create/revoke, plan change, deletion).
- [ ] Tenant-scoped (FEAT-01) so an org admin can view their org's activity in the UI.
- [ ] Retention/pruning policy is configurable.
- [ ] Tests assert key actions produce the expected audit entries.

## Implementation notes
- Consider writing via the FEAT-13 background queue to keep the hot path fast.
- Keep the actor/target as stable identifiers so log survives renames/deletes.

## Out of scope
- SIEM export/streaming, tamper-evident hash chaining (enterprise follow-ups).
