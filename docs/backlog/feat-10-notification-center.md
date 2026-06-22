# FEAT-10: In-app notification center & preferences

- **Track:** Feature · **Priority:** P1 · **Effort:** L · **Depends on:** — · **Status:** Backlog

## Problem / Why
Communication is email-only and one-directional. There's no in-app notification
center, no per-channel/per-type preferences beyond a single marketing-consent flag,
and no real-time delivery. Users expect a bell/inbox with read state and granular
notification controls.

## Current state (in this repo)
- `Email/ResendEmailService.cs` sends magic-link/onboarding/dunning mail; the only
  preference is `ApplicationUser.MarketingConsent`.
- Blazor Server already holds a SignalR circuit (usable for real-time push).
- No persistent notification entity, inbox UI, or preference matrix.

## Acceptance criteria
- [ ] `Notification` entity (recipient, type, title, body, link, read-at, created) + migration.
- [ ] A notification service that can fan out an event to enabled channels (in-app +
      email; extensible to push/Slack) honoring user preferences.
- [ ] In-app notification center in the shell (bell with unread count, list, mark read/all read),
      updating in near-real-time over the existing SignalR circuit.
- [ ] A preferences matrix in Settings (per notification type × channel) replacing the
      single marketing toggle; CAN-SPAM unsubscribe still honored for marketing email.
- [ ] At least two real notification types wired (e.g. payment failed, member invited).
- [ ] Tests for fan-out, preference filtering, and read-state.

## Implementation notes
- Distinguish **transient toasts** (UX-04) from **persistent notifications** (this story);
  some events may produce both.
- Reuse the FEAT-13 background queue for delivery; keep email templates localizable (UX-08).

## Out of scope
- Web push / mobile push infrastructure, digest scheduling (could be a later story).
