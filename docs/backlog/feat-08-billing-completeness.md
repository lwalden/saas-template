# FEAT-08: Billing completeness (annual, trials, coupons, tax, invoices)

- **Track:** Feature · **Priority:** P1 · **Effort:** L · **Depends on:** — · **Status:** Backlog

## Problem / Why
Billing covers checkout/portal/proration/dunning well, but several revenue-relevant
capabilities are missing or half-wired. Closing these makes the billing layer
genuinely product-ready.

## Current state (in this repo)
- `Billing/TierPriceResolver.cs` **knows annual price IDs** (reverse map) but
  `Billing/BillingEndpoints.cs` checkout only uses **monthly** config keys — annual isn't selectable.
- No trial creation flow (status `trialing` exists but is never initiated).
- No coupon/promo code support, no tax handling, no in-app invoice/receipt history
  (relies entirely on the Stripe portal).
- Seat counts exist in `TierLimits.cs` but seat-based billing isn't implemented.

## Acceptance criteria
- [ ] Annual interval is selectable end-to-end at checkout and reflected in Pricing/Billing UI.
- [ ] Configurable free trial (length per tier) with trial→paid conversion and trial-ending email.
- [ ] Promo/coupon codes accepted at checkout (via Stripe) and surfaced in the UI.
- [ ] Tax handling enabled (e.g. Stripe Tax) including tax-ID collection where required.
- [ ] In-app invoice/receipt history (list + download) sourced from Stripe.
- [ ] (If FEAT-01 done) seat-based billing tied to member count and `TierLimits.Seats`.
- [ ] Webhook handler (`Billing/StripeWebhookHandler.cs`) updated for any new events
      (e.g. `customer.subscription.trial_will_end`, invoice events) idempotently.
- [ ] Tests for annual checkout, trial lifecycle, coupon application, and invoice listing.

## Implementation notes
- Prefer Stripe-native features (Tax, Promotion Codes, Trials) over custom logic.
- Keep the existing idempotency/dunning patterns; extend, don't replace.

## Out of scope
- Usage-metered pricing internals (FEAT-07), enterprise quoting/contracts.
