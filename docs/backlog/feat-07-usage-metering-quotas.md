# FEAT-07: Usage metering & quota enforcement

- **Track:** Feature · **Priority:** P0 · **Effort:** M · **Depends on:** — · **Status:** Backlog

## Problem / Why
Tiered quotas are defined but **not enforced**: the subscription endpoint returns a
hard-coded `0` used with an explicit `TODO`. Any metered/quota-based SaaS needs real
usage counting, enforcement at the boundary, and visibility in the UI.

## Current state (in this repo)
- `Billing/BillingEndpoints.cs` (~line 389) returns `used = 0` with a comment:
  *"TODO: Replace 0 with your actual usage query."*
- `Billing/TierLimits.cs` defines `MonthlyQuota` per tier (0/100/1000/unlimited) — unused.
- `Components/Pages/Billing.razor` renders a usage `progressbar` fed by that stub.

## Acceptance criteria
- [ ] A `UsageEvent`/counter model and a `IUsageService` to record and query usage per
      user/org per billing period, with an EF migration.
- [ ] A reusable enforcement primitive (`CheckQuota`/middleware/filter) that blocks or
      soft-warns when `MonthlyQuota` is exceeded, with a clear upgrade CTA.
- [ ] Replace the `0` stub so the Billing usage bar reflects real usage; "unlimited" tier
      handled correctly.
- [ ] Usage resets/anchors to the Stripe billing period (`CurrentPeriodEnd`).
- [ ] Optional: report usage to Stripe for metered prices (document the toggle).
- [ ] Tests for counting, period rollover, limit enforcement, and unlimited tier.

## Implementation notes
- Keep recording cheap (counter increment, optionally a periodic rollup) so the hot path
  isn't slowed; back with caching (FEAT-13/infra) if needed.
- Define the canonical "billable unit" as a clearly marked extension point.

## Out of scope
- Full metered-billing pricing UX (overlaps FEAT-08), per-feature multi-meter analytics.
