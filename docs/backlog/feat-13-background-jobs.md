# FEAT-13: Background-job & scheduling infrastructure

- **Track:** Feature · **Priority:** P1 · **Effort:** M · **Depends on:** — · **Status:** Backlog

## Problem / Why
Async work is handled by a single hand-rolled `BackgroundService` that polls hourly.
There's no durable queue, retries, scheduling, or visibility, so adding more async
work (emails, audit writes, notification fan-out, usage rollups) means more bespoke
loops. A real job system is shared infrastructure many other stories depend on.

## Current state (in this repo)
- `Monitoring/OnboardingEmailService.cs` is an hourly `BackgroundService`; failures are
  logged and simply retried next hour — no durable enqueue, no backoff, no dead-letter.
- `Webhooks/WebhookDispatcher.cs` fires outbound HTTP inline with a 5s timeout and no retry.
- No `IMemoryCache`/distributed cache registered in `Program.cs`.

## Acceptance criteria
- [ ] A durable background-job mechanism (enqueue, scheduled/recurring, automatic retry with
      backoff, dead-letter) suitable for this stack and Azure Container Apps.
- [ ] The onboarding drip is migrated onto it (or kept but enqueuing real jobs) without losing
      the existing day-0/3/7 semantics and `MarketingConsent` checks.
- [ ] Outbound webhook dispatch gains retry/backoff and delivery-status tracking.
- [ ] A caching abstraction is registered (in-memory now, swappable to Redis) for hot reads.
- [ ] Basic job visibility (logs/metrics, and admin view if FEAT-12 exists).
- [ ] Tests for enqueue/execute, retry-on-failure, and recurring schedule.

## Implementation notes
- Options: Hangfire (DB-backed, simplest here), Quartz.NET, or a queue + worker. Choose one
  and document why; ensure it works with the single-container deploy model.
- This unblocks FEAT-07 rollups, FEAT-09 audit writes, and FEAT-10 notification fan-out.

## Out of scope
- Full message-bus/event-driven architecture across services.
