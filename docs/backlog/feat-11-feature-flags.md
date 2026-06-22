# FEAT-11: Feature flags & gradual rollout

- **Track:** Feature · **Priority:** P2 · **Effort:** M · **Depends on:** — · **Status:** Backlog

## Problem / Why
There's no way to ship code dark, gate features by plan/tier, or roll out gradually.
Feature flags are a standard SaaS capability that de-risks releases and enables
plan-gated and percentage rollouts.

## Current state (in this repo)
- No flag system. `Billing/TierLimits.cs` is the only "what can this account do" signal,
  and it's quota-only, not capability gating.

## Acceptance criteria
- [ ] A flag abstraction (`IFeatureService.IsEnabled(flag, context)`) evaluating by:
      global on/off, plan/tier, organization/user targeting, and percentage rollout.
- [ ] Flags configurable without redeploy (config + DB-backed store; cached for the hot path).
- [ ] A Blazor helper/component to conditionally render flagged UI, and a server-side guard.
- [ ] At least one tier-gated example feature demonstrating plan gating from `TierLimits`.
- [ ] (If FEAT-12 done) admin UI to toggle flags and set targeting.
- [ ] Tests for evaluation precedence and percentage bucketing stability.

## Implementation notes
- `Microsoft.FeatureManagement` is a natural fit for this stack; add custom filters for
  tier/org targeting. Keep evaluation deterministic per user for percentage rollouts.

## Out of scope
- Full experimentation/A-B analytics platform.
