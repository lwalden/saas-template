# Backlog — 2026 Standards & SaaS Feature Assessment

This backlog is the output of an assessment of the SaaS template against
**(a) 2026 UI/UX design standards** and **(b) generally useful SaaS feature gaps**.

Each story is self-contained and written so a future Claude Code session can pick
**one file** and implement it end-to-end (it cites the relevant current files,
gives acceptance criteria, and notes the intended approach). Stories are grouped
into two tracks: **UX** (`ux-*`) and **Feature** (`feat-*`).

## How to use this backlog

1. Pick a story file (start with `P0`, respect `Depends on`).
2. Implement on a `feature/<id>-<slug>` branch per `CLAUDE.md`.
3. Keep the design-token system, accessibility, and test conventions intact.
4. Check the acceptance criteria boxes and update **Status** when done.

## Assessment summary

**What the template already does well.** Solid passwordless + OAuth auth, Stripe
billing with idempotent webhooks, Resend email with CAN‑SPAM unsubscribe, a real
Tailwind v4 design-token system (`input.css`), a WCAG‑2.1‑AA‑aware Blazor shell
(skip links, ARIA, focus traps), rate limiting, security headers, an SSRF guard,
and an xUnit integration harness. It genuinely starts a product at "week 8."

**The biggest gaps (at time of assessment).**

- **UX:** no dark mode/theming, no toast/skeleton/empty-state primitives, no
  documented type/spacing scale or reusable component layer, no command palette,
  no i18n, and inconsistent reduced-motion/focus coverage.
- **Features:** single-user only (no teams/orgs/multi-tenancy/RBAC), missing
  password-reset & email-verification flows, no MFA, usage metering is a literal
  `TODO` stub, no audit log, no in-app notifications, no admin UI, no structured
  logging/telemetry, and no background-job or file-storage infrastructure.

> _Progress since assessment: password reset & email verification (back-end +
> pages), the audit log, observability, real usage metering & quota enforcement,
> an OpenAPI/v1 contract, and a dark-mode theming foundation have shipped
> (FEAT-03, FEAT-09, FEAT-14, FEAT-07, FEAT-16, UX-01 — see Sprint log below).
> The remaining gaps above are still open._

## Sprint log

**Sprint 1 — "Auth completeness + Audit foundation" (PR #3, merged).** Delivered
**FEAT-03** (password reset & email verification) and **FEAT-09** (audit log
foundation) end-to-end with tests (suite 173 → 190 passing). Both stories' files
carry a `Shipped`/`Deferred` note documenting exactly what landed. Deferred slices
were folded back into existing backlog items rather than dropped:
- FEAT-03 → user-facing Blazor reset/verify **pages** and gating unverified accounts
  from sensitive actions remain open (small follow-up; not yet a separate story).
- FEAT-09 → durable async write path is **FEAT-13**; tenant-scoped audit **UI** needs
  **FEAT-01** + **FEAT-12**; retention/pruning remains in the FEAT-09 file.

**Sprint 2 — "Operability + monetization + theming" (branch `claude/status-check-e55d1m`).**
Delivered five stories end-to-end, each integrated and verified independently
(suite 190 → 207 passing; the 4 perennial failures are environmental Stripe-egress
timeouts in the sandbox, green on CI):
- **FEAT-14** — structured logging, OpenTelemetry tracing/metrics, liveness/readiness
  health split, lightweight error tracking. OTLP export is env-var-gated and off under test.
- **FEAT-16** — code-first OpenAPI v1 document at `/openapi/v1.json` + Scalar UI
  (non-prod), endpoint annotations, JWT security scheme, ops/infra excluded. No route rewrites.
- **FEAT-07** — `UsageEvent` table + `IUsageService` (record/query/enforce) anchored to
  the Stripe billing period, `.RequireQuota()` filter (402 + upgrade CTA), unlimited tier
  handled; replaced the `used = 0` stub so the Billing bar shows real usage. EF migration added.
- **UX-01** — semantic CSS-custom-property theming with a WCAG-AA dark palette, pre-paint
  bootstrap (no FOUC), system/explicit resolution, reduced-motion guard, temporary toggle in Settings.
- **FEAT-03 follow-up** — the deferred reset/verify/forgot-password Blazor pages + login entry point.
  Server-side gating of unverified accounts remains open.

## Stories

### Track A — 2026 UI/UX design standards

| ID | Title | Priority | Effort | Depends on | Status |
|----|-------|----------|--------|-----------|--------|
| [UX-01](ux-01-dark-mode-theming.md) | Dark mode & theming foundation | P0 | L | — | ✅ Done (Sprint 2) |
| [UX-02](ux-02-appearance-preferences.md) | Appearance & accessibility preferences | P1 | M | UX-01 | Backlog |
| [UX-03](ux-03-component-library-tokens.md) | Reusable component library & documented design tokens | P1 | L | — | Backlog |
| [UX-04](ux-04-feedback-state-components.md) | Toasts, skeleton loaders & empty states | P1 | M | UX-03 | Backlog |
| [UX-05](ux-05-command-palette.md) | Command palette (⌘K) & keyboard shortcuts | P2 | M | UX-03 | Backlog |
| [UX-06](ux-06-accessibility-hardening.md) | WCAG 2.2 AA hardening + automated a11y CI gate | P1 | M | — | Backlog |
| [UX-07](ux-07-form-ux-system.md) | Form UX system (validation summary, async, help) | P2 | M | UX-03 | Backlog |
| [UX-08](ux-08-i18n-localization.md) | Internationalization & localization readiness | P2 | L | — | Backlog |

### Track B — SaaS feature gaps

| ID | Title | Priority | Effort | Depends on | Status |
|----|-------|----------|--------|-----------|--------|
| [FEAT-01](feat-01-teams-organizations.md) | Teams / organizations & multi-tenancy | P0 | XL | — | Backlog |
| [FEAT-02](feat-02-invitations-rbac.md) | Member invitations & RBAC | P1 | L | FEAT-01 | Backlog |
| [FEAT-03](feat-03-password-reset-email-verification.md) | Password reset & email verification | P0 | M | — | ✅ Done (PR #3 + Sprint 2 pages) |
| [FEAT-04](feat-04-mfa-totp.md) | MFA / TOTP two-factor auth | P1 | M | FEAT-03 | Backlog |
| [FEAT-05](feat-05-account-security.md) | Session management, more OAuth, account deletion/GDPR export | P1 | L | — | Backlog |
| [FEAT-06](feat-06-end-user-api-keys.md) | End-user API keys & developer access | P1 | M | — | Backlog |
| [FEAT-07](feat-07-usage-metering-quotas.md) | Usage metering & quota enforcement | P0 | M | — | ✅ Done (Sprint 2) |
| [FEAT-08](feat-08-billing-completeness.md) | Billing completeness (annual, trials, coupons, tax, invoices) | P1 | L | — | Backlog |
| [FEAT-09](feat-09-audit-log.md) | Audit log & activity trail | P1 | M | — | ✅ Done (PR #3) |
| [FEAT-10](feat-10-notification-center.md) | In-app notification center & preferences | P1 | L | — | Backlog |
| [FEAT-11](feat-11-feature-flags.md) | Feature flags & gradual rollout | P2 | M | — | Backlog |
| [FEAT-12](feat-12-admin-dashboard.md) | Admin dashboard UI | P2 | M | — | Backlog |
| [FEAT-13](feat-13-background-jobs.md) | Background-job & scheduling infrastructure | P1 | M | — | Backlog |
| [FEAT-14](feat-14-observability.md) | Observability: structured logging, OpenTelemetry, error tracking | P1 | M | — | ✅ Done (Sprint 2) |
| [FEAT-15](feat-15-file-uploads.md) | File uploads & blob storage | P2 | M | — | Backlog |
| [FEAT-16](feat-16-openapi-versioning.md) | OpenAPI/Swagger docs & API versioning | P2 | S | — | ✅ Done (Sprint 2) |

**Legend** — Priority: `P0` table-stakes/unblocks others, `P1` high value,
`P2` valuable polish. Effort: `S` < ½ day, `M` ~1 day, `L` 2–3 days, `XL` multi-session.
