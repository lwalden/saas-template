# FEAT-15: File uploads & blob storage

- **Track:** Feature · **Priority:** P2 · **Effort:** M · **Depends on:** — · **Status:** Backlog

## Problem / Why
There's no file-handling capability — not even user avatars. Most SaaS needs to
accept uploads (avatars, logos, attachments) with safe storage and serving. A small
storage abstraction plus one real use case establishes the pattern.

## Current state (in this repo)
- No blob-storage client or upload endpoint; `wwwroot` static files only.
- `ApplicationUser` has no avatar; `Organization` (FEAT-01) would want a logo.
- `Security/SsrfGuard.cs` exists and should be reused for any URL-based ingestion.

## Acceptance criteria
- [ ] An `IFileStorage` abstraction (put/get/delete/signed-URL) with an Azure Blob
      implementation (matches the Azure deploy target) and a local-disk dev implementation.
- [ ] An upload endpoint enforcing content-type allow-list, size limits, and basic safety;
      stored objects are tenant/user-scoped (FEAT-01) and access-controlled.
- [ ] Serve via short-lived signed URLs (no public-by-default buckets).
- [ ] One real use case implemented end-to-end: user avatar (and/or org logo) with UI in Settings.
- [ ] Any server-side fetch of remote URLs routes through `SsrfGuard`.
- [ ] Tests for validation (rejects bad type/oversize) and round-trip store/retrieve.

## Implementation notes
- Stream uploads; don't buffer large files in memory. Strip/normalize image metadata for avatars.
- Keep the storage interface provider-agnostic so S3/GCS can be added later.

## Out of scope
- Image processing pipeline/CDN config, virus scanning (note as a future hardening step).
