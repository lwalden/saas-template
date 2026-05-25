# CLAUDE.md — SaaS Template

> Claude reads this file at the start of every session. Keep it concise.
> Replace the placeholders below when you start a real product from this template.

## Project Identity

**Project:** _<your product name>_
**Description:** _<one line: what it does, for whom>_
**Type:** web-app
**Stack:** C# / .NET 10 / Blazor Server / EF Core + Azure SQL / Stripe / Resend / Azure Container Apps

## Stack notes

- ASP.NET Core Identity (email/password + magic link + Google OAuth), JWT bearer for API.
- Stripe subscriptions with idempotent webhooks (see `Billing/`).
- Resend for transactional email (see `Email/`).
- Blazor Server with a Tailwind design-token system (`input.css` → `style.css`).

## Behavioral Rules

### Git Workflow
- Never commit directly to `main` — branch first.
- Branches: `feature/`, `fix/`, `chore/` + short description.
- Commits: `type(scope): description` — explain why, not what.
- Open PRs for changes; don't merge without explicit approval.

### Credentials
- Never commit secrets. Use `.env` (gitignored) locally; real env vars / secrets in deployment.

### Verification
- Build + run the test suite before every commit: `dotnet test tests/SaasTemplate.Api.Tests --filter "Category!=SqlServer&Category!=Smoke"`.

### Conventions
- Add domain entities to `Data/Entities.cs` + `AppDbContext`, then create an EF migration.
- Keep UI accessible (WCAG 2.1 AA) and consistent with the existing design tokens.
