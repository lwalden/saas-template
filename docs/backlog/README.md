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

**The biggest gaps.**

- **UX:** no dark mode/theming, no toast/skeleton/empty-state primitives, no
  documented type/spacing scale or reusable component layer, no command palette,
  no i18n, and inconsistent reduced-motion/focus coverage.
- **Features:** single-user only (no teams/orgs/multi-tenancy/RBAC), missing
  password-reset & email-verification flows, no MFA, usage metering is a literal
  `TODO` stub, no audit log, no in-app notifications, no admin UI, no structured
  logging/telemetry, and no background-job or file-storage infrastructure.

## Stories

### Track A — 2026 UI/UX design standards

| ID | Title | Priority | Effort | Depends on |
|----|-------|----------|--------|-----------|
| [UX-01](ux-01-dark-mode-theming.md) | Dark mode & theming foundation | P0 | L | — |
| [UX-02](ux-02-appearance-preferences.md) | Appearance & accessibility preferences | P1 | M | UX-01 |
| [UX-03](ux-03-component-library-tokens.md) | Reusable component library & documented design tokens | P1 | L | — |
| [UX-04](ux-04-feedback-state-components.md) | Toasts, skeleton loaders & empty states | P1 | M | UX-03 |
| [UX-05](ux-05-command-palette.md) | Command palette (⌘K) & keyboard shortcuts | P2 | M | UX-03 |
| [UX-06](ux-06-accessibility-hardening.md) | WCAG 2.2 AA hardening + automated a11y CI gate | P1 | M | — |
| [UX-07](ux-07-form-ux-system.md) | Form UX system (validation summary, async, help) | P2 | M | UX-03 |
| [UX-08](ux-08-i18n-localization.md) | Internationalization & localization readiness | P2 | L | — |

### Track B — SaaS feature gaps

| ID | Title | Priority | Effort | Depends on |
|----|-------|----------|--------|-----------|
| [FEAT-01](feat-01-teams-organizations.md) | Teams / organizations & multi-tenancy | P0 | XL | — |
| [FEAT-02](feat-02-invitations-rbac.md) | Member invitations & RBAC | P1 | L | FEAT-01 |
| [FEAT-03](feat-03-password-reset-email-verification.md) | Password reset & email verification | P0 | M | — |
| [FEAT-04](feat-04-mfa-totp.md) | MFA / TOTP two-factor auth | P1 | M | FEAT-03 |
| [FEAT-05](feat-05-account-security.md) | Session management, more OAuth, account deletion/GDPR export | P1 | L | — |
| [FEAT-06](feat-06-end-user-api-keys.md) | End-user API keys & developer access | P1 | M | — |
| [FEAT-07](feat-07-usage-metering-quotas.md) | Usage metering & quota enforcement | P0 | M | — |
| [FEAT-08](feat-08-billing-completeness.md) | Billing completeness (annual, trials, coupons, tax, invoices) | P1 | L | — |
| [FEAT-09](feat-09-audit-log.md) | Audit log & activity trail | P1 | M | — |
| [FEAT-10](feat-10-notification-center.md) | In-app notification center & preferences | P1 | L | — |
| [FEAT-11](feat-11-feature-flags.md) | Feature flags & gradual rollout | P2 | M | — |
| [FEAT-12](feat-12-admin-dashboard.md) | Admin dashboard UI | P2 | M | — |
| [FEAT-13](feat-13-background-jobs.md) | Background-job & scheduling infrastructure | P1 | M | — |
| [FEAT-14](feat-14-observability.md) | Observability: structured logging, OpenTelemetry, error tracking | P1 | M | — |
| [FEAT-15](feat-15-file-uploads.md) | File uploads & blob storage | P2 | M | — |
| [FEAT-16](feat-16-openapi-versioning.md) | OpenAPI/Swagger docs & API versioning | P2 | S | — |

**Legend** — Priority: `P0` table-stakes/unblocks others, `P1` high value,
`P2` valuable polish. Effort: `S` < ½ day, `M` ~1 day, `L` 2–3 days, `XL` multi-session.
