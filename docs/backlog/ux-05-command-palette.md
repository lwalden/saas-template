# UX-05: Command palette (⌘K) & keyboard shortcuts

- **Track:** UX · **Priority:** P2 · **Effort:** M · **Depends on:** UX-03 · **Status:** Backlog

## Problem / Why
A ⌘K/Ctrl-K command palette and a small set of global shortcuts are a 2026
power-user staple. They make navigation and primary actions reachable without a
mouse and scale better than menus as the app grows.

## Current state (in this repo)
- Navigation is sidebar-only (`Components/Layout/MainLayout.razor`); no global
  keyboard affordances beyond Escape closing dropdowns/dialogs.
- `App.razor` already wires global JS interop — a place to register key handlers.

## Acceptance criteria
- [ ] ⌘K / Ctrl-K opens a searchable command palette overlay (dialog semantics:
      `role="dialog"`, `aria-modal`, focus trap, Escape closes).
- [ ] Palette lists navigation targets and key actions (e.g. Go to Billing, Settings,
      Toggle theme, Sign out), filtered as you type, arrow-key + Enter to run.
- [ ] A discoverable shortcut for the palette and a `?` shortcuts cheat-sheet.
- [ ] Registry is extensible so future features can contribute commands.
- [ ] Fully keyboard operable and screen-reader friendly; respects reduced motion.

## Implementation notes
- Reuse the UX-03 `Dialog` and the existing `trapFocus`/`releaseFocus` helpers.
- Keep a C# command registry (label, keywords, action, optional `[Authorize]`),
  so commands are filtered by the user's permissions (FEAT-02) when available.

## Out of scope
- Full fuzzy/full-text search over user data (relates to a future search story).
