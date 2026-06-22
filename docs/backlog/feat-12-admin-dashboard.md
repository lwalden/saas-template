# FEAT-12: Admin dashboard UI

- **Track:** Feature · **Priority:** P2 · **Effort:** M · **Depends on:** — · **Status:** Backlog

## Problem / Why
Operational tooling exists only as raw API endpoints guarded by an `X-Api-Key`.
There's no internal UI for staff to view metrics, find a user, or take support
actions, so operators resort to curl. An admin dashboard turns existing capability
into a usable surface.

## Current state (in this repo)
- `Monitoring/OpsEndpoints.cs` already exposes: status/metrics (users, subscriptions,
  MRR), list users, set tier, cleanup test users — all **API-only**, `X-Api-Key` guarded.
- No admin role or admin UI; metrics aren't visualized.

## Acceptance criteria
- [ ] An authenticated, authorized `/admin` area gated by an admin role/policy
      (coordinate with FEAT-02 RBAC) — not just the shared ops key.
- [ ] Overview page visualizing the existing ops metrics (signups, active/past-due subs, MRR,
      recent activity) reusing `OpsEndpoints` data.
- [ ] User search/detail with safe support actions (view subscription, set tier, resend
      verification, impersonate-with-audit if added) — all writing to the audit log (FEAT-09).
- [ ] Sensitive admin actions are confirmed and audited; least-privilege by default.
- [ ] Tests for authorization (non-admins blocked) and a representative admin action.

## Implementation notes
- Reuse existing ops queries; don't duplicate metric logic — refactor it into a service
  consumed by both the API and the UI.
- Build on the UX-03 component library for tables/cards.

## Out of scope
- Full BI/analytics, billing reconciliation tooling.
