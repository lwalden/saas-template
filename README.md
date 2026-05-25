# SaaS Template

A production-shaped SaaS starter on **.NET 10 / Blazor Server**, extracted from a real
deployed product. It gives you the customer-agnostic plumbing — auth, subscription billing,
email, app shell, ops, security, CI/CD, and a test harness — so a new product starts at
"week 8," not week 0.

## What's included

| Area | Details |
|---|---|
| **Auth** | ASP.NET Core Identity — email/password, magic link, and Google OAuth (incl. the Blazor-prerender cookie-handoff pattern). JWT bearer for API calls. |
| **Billing** | Stripe subscriptions — checkout, customer portal, proration, dunning, idempotent webhooks, tiered plans (`TierLimits`). |
| **Email** | Resend transactional email with a CAN-SPAM unsubscribe flow and marketing-consent toggle. |
| **UI** | Blazor Server shell (mobile drawer, profile dropdown, breadcrumbs), a Tailwind design-token system, and Login/Pricing/Billing/Settings/Home pages. |
| **Ops & security** | Health + API-key-guarded ops endpoints, sliding-window rate limiting, security headers, an SSRF guard, and an outbound webhook dispatcher. |
| **Data** | EF Core + Azure SQL, Identity tables, `SubscriptionEntity`, DB-persisted Data Protection keys. |
| **CI/CD** | GitHub Actions: `ci.yml` (build + test on push/PR) plus manual-trigger Azure Container Apps deploy / prod-promotion / smoke-test workflows. |
| **Tests** | xUnit integration tests over a SQLite in-memory harness (`WebApplicationFactory`). |

## Prerequisites

- .NET 10 SDK
- SQL Server (LocalDB, a container, or Azure SQL) for running the app
- Node.js (only to rebuild Tailwind CSS from `input.css` → `style.css`)

## Quickstart

```bash
cp .env.example .env          # fill in your values (see table below)
dotnet restore SaasTemplate.sln
dotnet build SaasTemplate.sln
dotnet test tests/SaasTemplate.Api.Tests --filter "Category!=SqlServer&Category!=Smoke"
dotnet run --project src/SaasTemplate.Api
```

EF migrations:

```bash
dotnet tool install --global dotnet-ef          # once
dotnet ef migrations add <Name> --project src/SaasTemplate.Api
dotnet ef database update --project src/SaasTemplate.Api
```

## Configuration

All config is environment variables (see `.env.example`). Key ones:
`ConnectionStrings__DefaultConnection`, `JWT_SECRET`, `STRIPE_SECRET_KEY`,
`STRIPE_WEBHOOK_SECRET`, `STRIPE_PRICE_ID_*`, `RESEND_API_KEY`,
`GOOGLE_CLIENT_ID` / `GOOGLE_CLIENT_SECRET`, `APP_BASE_URL`, `APP_FRONTEND_URL`,
`INTERNAL_BASE_URL`, `OPS_API_KEY`.

In Development, a `.env` at the repo root is auto-loaded. In production, set real
environment variables / secrets. `JWT_SECRET` and `OPS_API_KEY` must be ≥32 chars.

## CI/CD

- **`ci.yml`** runs automatically on push/PR to `main` — restore, build, test. It passes out of the box.
- **`deploy.yml`**, **`promote-to-prod.yml`**, **`smoke-test-prod.yml`** are **manual-trigger only**. They target Azure Container Apps and contain `# TODO` placeholders for resource names and domains. Before using them, set the resource names + repo secrets (`AZURE_CREDENTIALS`, `GHCR_PAT`, optionally `VERCEL_DEPLOY_HOOK_URL`).

## Using this for a new product

1. Find/replace `SaasTemplate` → your product name (namespaces, `.sln`, `.csproj`, Dockerfile).
2. Set product identity in `CLAUDE.md` and rebrand the static pages + `input.css` design tokens.
3. Re-tier `Billing/TierLimits.cs` and your Stripe price IDs.
4. Add your domain entities to `Data/Entities.cs` + `AppDbContext`, then create a migration.
5. Build your features on top of the auth/billing/email/shell foundation.

## License

Add your license here.
