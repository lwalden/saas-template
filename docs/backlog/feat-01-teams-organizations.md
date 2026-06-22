# FEAT-01: Teams / organizations & multi-tenancy

- **Track:** Feature · **Priority:** P0 · **Effort:** XL · **Depends on:** — · **Status:** Backlog

## Problem / Why
The template is strictly single-user: a `SubscriptionEntity` belongs to one
`ApplicationUser`. Most B2B SaaS needs an organization/workspace boundary that owns
the subscription and contains members and data. This is the single largest
structural gap and a prerequisite for invitations, RBAC, seat billing, and
tenant-scoped audit logs. It is intentionally foundational and likely multi-session.

## Current state (in this repo)
- `Data/Entities.cs` — `ApplicationUser` ⇄ `SubscriptionEntity` one-to-one; no org concept.
- `Billing/TierLimits.cs` already encodes **seats** per tier (1/1/5/25) but nothing consumes them.
- All endpoints resolve the current user, never a tenant.

## Acceptance criteria
- [ ] New entities: `Organization` (id, name, slug, created) and `OrganizationMember`
      (org, user, role, status), with EF migration. Subscription moves to belong to the
      **organization** (migrate the existing user→subscription relationship).
- [ ] On signup, a personal organization is auto-created so single-user flows still work.
- [ ] A "current organization" is resolved per request/circuit and exposed to UI;
      users in multiple orgs can switch (org switcher in `MainLayout`).
- [ ] Data access is tenant-scoped: a reusable pattern (e.g. EF global query filter
      keyed on current org) so future entities are isolated by default.
- [ ] Billing, ops, and onboarding reference the org's subscription, not the user's.
- [ ] Tests cover org creation, switching, and tenant isolation.

## Implementation notes
- Decide tenancy model explicitly (shared-DB row-level isolation recommended for this
  stack) and document it; wire a `ICurrentTenant`/`OrgContext` scoped service.
- Sequence: schema + migration → tenant context → move subscription → UI switcher.
- This unblocks FEAT-02 (invites/RBAC) and seat-based billing in FEAT-08.

## Out of scope
- Cross-org data sharing, org-level SSO/SCIM (enterprise follow-ups).
