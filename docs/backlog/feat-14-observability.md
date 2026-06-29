# FEAT-14: Observability — structured logging, OpenTelemetry, error tracking

- **Track:** Feature · **Priority:** P1 · **Effort:** M · **Depends on:** — · **Status:** ✅ Done (Sprint 2)

> **Shipped:** structured logging with correlation ids + user/org scope enrichment
> (JSON sink in prod / `LOG_JSON`), OpenTelemetry tracing + metrics (ASP.NET Core,
> HttpClient, EF Core, runtime) with OTLP export gated on `OTEL_EXPORTER_OTLP_ENDPOINT`,
> a `/healthz` + `/healthz/live` + `/healthz/ready` liveness/readiness split, request-
> logging middleware (no query strings/PII), and a lightweight unhandled-exception
> tracker (`SENTRY_DSN` documented as the swap-in toggle). See `docs/observability.md`.
> Covered by `HealthCheckTests`.
> **Deferred:** provider-specific dashboards/alerting and SLOs (out of scope).

## Problem / Why
Production readiness is limited: only a bare `/healthz`, default logging, and no
tracing, metrics, or error tracking. When something breaks in production there's no
correlation, no traces, and no aggregated errors. This is foundational for operating
the template as a real service.

## Current state (in this repo)
- `Program.cs` calls `AddHealthChecks()` with **no custom checks** and maps `/healthz`.
- Default console logging; **no Serilog/structured sink**, no `ActivitySource`, no metrics.
- No error tracking (Sentry/etc.); CSP and security headers are present (good).

## Acceptance criteria
- [ ] Structured logging with correlation/request IDs and consistent enrichment
      (user/org/tenant when available), JSON sink configurable for production.
- [ ] OpenTelemetry tracing + metrics wired (ASP.NET Core, HttpClient, EF Core
      instrumentation) with OTLP export configurable via env vars.
- [ ] Meaningful health checks (database, and key dependencies like Stripe/Resend reachability)
      surfaced via `/healthz` with a readiness/liveness split.
- [ ] Error tracking integration (e.g. Sentry) capturing unhandled exceptions with release/env tags.
- [ ] Request-logging middleware that records method/route/status/latency without logging secrets/PII.
- [ ] Docs note the env vars and how to point at a collector; tests/build stay green.

## Implementation notes
- Prefer OpenTelemetry-native packages so any backend (Azure Monitor, Grafana, Datadog) works.
- Be careful not to log JWTs, Stripe payloads, or email addresses in cleartext.

## Out of scope
- Dashboards/alerting config (provider-specific), SLO definitions.
