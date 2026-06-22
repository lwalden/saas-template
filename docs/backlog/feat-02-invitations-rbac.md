# FEAT-02: Member invitations & RBAC

- **Track:** Feature · **Priority:** P1 · **Effort:** L · **Depends on:** FEAT-01 · **Status:** Backlog

## Problem / Why
Once organizations exist, teams need to invite members and control what each can do.
ASP.NET Identity's role tables are present but unused, and there is no
authorization policy layer. Without RBAC, every member is effectively an admin.

## Current state (in this repo)
- Identity `AspNetRoles`/`AspNetUserRoles` tables exist (migrated) but are **unused**.
- `Program.cs` registers authorization with **no policies**.
- No invitation entity or email (the email layer exists and is reusable).

## Acceptance criteria
- [ ] Org-scoped roles (e.g. Owner, Admin, Member; optionally Billing-only) defined as
      app-level roles on `OrganizationMember`, plus a permission mapping.
- [ ] Authorization policies/handlers enforce permissions on endpoints and UI (hide/disable
      actions a member can't perform); a tenant-aware `[Authorize]` pattern.
- [ ] Invitation flow: invite by email → tokenized, expiring invite → accept creates/links
      membership; invite + reminder emails via `Email/ResendEmailService.cs`.
- [ ] Member management UI: list members, change roles, remove, revoke pending invites.
- [ ] Seat limits from `TierLimits.cs` enforced on invite/accept; over-limit is blocked
      with a clear upgrade path.
- [ ] Tests for permission enforcement, invite accept/expire, and seat limits.

## Implementation notes
- Prefer a permission-based policy model (roles → permissions) so it scales beyond 3 roles.
- Reuse the HMAC token pattern from `Auth/UnsubscribeToken.cs` for invite tokens.

## Out of scope
- SCIM/directory sync, custom per-resource ACLs.
