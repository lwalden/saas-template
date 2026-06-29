# Observability

The API ships with structured logging, OpenTelemetry tracing + metrics, health
checks, request logging, and an error-tracking integration point. Everything is
off-by-default for network export: with no OTLP endpoint configured the app runs
normally and performs no telemetry network I/O at startup.

## Structured logging

- Logs flow through `Microsoft.Extensions.Logging`. Scopes are included on every
  line, carrying a correlation id, request id, and (when authenticated) the user
  and org ids.
- TraceId / SpanId from the active activity are attached automatically.
- In **Production** (or when `LOG_JSON=true`) logs are emitted as single-line JSON
  via the JSON console formatter, suitable for a log shipper. Otherwise a
  human-readable console formatter is used.
- We never log JWTs, Stripe payloads, raw email addresses, query strings, headers,
  cookies, or request/response bodies. Request logging records only method, the
  matched route (or path component, never the query string), status, and latency.
- EF Core spans do **not** capture SQL statement text, so query parameters / PII
  never reach an exporter.

## OpenTelemetry tracing + metrics

Instrumentation is wired for ASP.NET Core, HttpClient, and EF Core (traces) plus
ASP.NET Core, HttpClient, and process/runtime metrics. Export uses the standard
OTLP exporter, configured entirely through the OpenTelemetry environment
variables. When `OTEL_EXPORTER_OTLP_ENDPOINT` is unset, no exporter is added.

| Env var | Purpose |
| --- | --- |
| `OTEL_EXPORTER_OTLP_ENDPOINT` | OTLP collector endpoint, e.g. `http://otel-collector:4317`. **Setting this enables export.** |
| `OTEL_EXPORTER_OTLP_PROTOCOL` | `grpc` (default) or `http/protobuf`. |
| `OTEL_EXPORTER_OTLP_HEADERS` | Extra headers, e.g. `api-key=...` for a vendor backend. |
| `OTEL_SERVICE_NAME` | Override the reported service name (defaults to `SaasTemplate.Api`). |
| `OTEL_RESOURCE_ATTRIBUTES` | Extra resource attributes, comma-separated `k=v`. |
| `LOG_JSON` | `true`/`false` — force JSON console logs (defaults to true in Production). |

The service resource always reports `service.name`, `service.version` (assembly
informational version), and `deployment.environment` (the ASP.NET Core env name).
Health probe requests (`/healthz*`) are excluded from traces.

Point at any OTLP-compatible backend (Azure Monitor via the OTLP collector,
Grafana/Tempo/Loki, Datadog, Honeycomb, etc.) by setting the vars above.

## Health checks

| Endpoint | Checks | Use |
| --- | --- | --- |
| `/healthz` | all checks | overall health (preserved for existing callers) |
| `/healthz/live` | trivial `self` check only | **liveness** — does not touch the DB |
| `/healthz/ready` | database (`AddDbContextCheck`) | **readiness** — gate traffic on dependencies |

Liveness intentionally avoids the DB so a transient DB blip doesn't cause the
orchestrator to restart an otherwise-healthy process. Readiness includes the DB so
load balancers stop routing until the dependency is reachable.

## Error tracking

Unhandled exceptions are captured by a middleware that records them through the
structured logger with `Environment` and `Release` tags and the active
trace/correlation id, then re-throws so normal ASP.NET Core error handling runs.
The exception is also recorded on the active span.

| Env var | Purpose |
| --- | --- |
| `SENTRY_DSN` | Conventional toggle. When set, errors are tagged `SentryDsnConfigured=true`. Swap in the Sentry SDK later by adding its package + middleware in `Monitoring/ErrorTracking.cs`; the env-var contract stays the same. |

## Correlation ids

Every response carries an `X-Correlation-ID` header. Inbound `X-Correlation-ID` or
`X-Request-ID` headers are honored; otherwise the active trace id (or the ASP.NET
request id) is used. The same value tags request logs and error reports.
