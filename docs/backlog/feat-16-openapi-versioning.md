# FEAT-16: OpenAPI/Swagger docs & API versioning

- **Track:** Feature · **Priority:** P2 · **Effort:** S · **Depends on:** — · **Status:** ✅ Done (Sprint 2)

> **Shipped:** code-first OpenAPI document (`Microsoft.AspNetCore.OpenApi`) for the
> public surface at `/openapi/v1.json`, a Scalar interactive UI at `/scalar` gated to
> non-production, endpoint annotations (names/summaries/tags/response types) on the
> Auth/Billing/Meta/Account endpoints, a registered JWT bearer security scheme (with a
> marked FEAT-06 API-key extension point), and ops/`/healthz`/webhook/unsubscribe
> excluded. The existing surface is documented as v1 with zero route rewrites; the
> v2 URL-segment/header path is the documented forward strategy. Covered by `OpenApiTests`.

## Problem / Why
The app exposes JSON/JWT API endpoints but publishes no machine-readable contract and
has no versioning strategy. An OpenAPI document + versioning makes the API consumable
(SDKs, Postman, partners) and lets it evolve without breaking clients — especially
relevant once end-user API keys (FEAT-06) exist.

## Current state (in this repo)
- Minimal-API endpoints across `Auth/`, `Billing/`, `Monitoring/` with **no OpenAPI/Swagger**
  and **no API version scheme**.

## Acceptance criteria
- [ ] OpenAPI document generated for the public API surface (e.g. `Microsoft.AspNetCore.OpenApi`),
      served at a stable path with a UI (Swagger UI / Scalar) in non-prod.
- [ ] Endpoints annotated with summaries, request/response types, and auth requirements so the
      spec is accurate; security schemes (JWT, and API key from FEAT-06) documented.
- [ ] An API versioning strategy applied (URL segment or header) with the current surface as v1.
- [ ] Internal/ops endpoints excluded from the public document.
- [ ] A test asserts the OpenAPI document builds and includes representative endpoints.

## Implementation notes
- Keep the spec generated from code (no hand-maintained YAML drift).
- Gate the interactive UI behind a non-production check.

## Out of scope
- Auto-generated client SDK publishing, a developer portal.
