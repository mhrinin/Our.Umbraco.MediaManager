# Our.Umbraco.MediaManager — Feature Proposal

**Package / root namespace:** `Our.Umbraco.MediaManager`

## Context
A recurring pain in Umbraco projects: media libraries accumulate files that are no longer
tied to any content, plus orphaned files on disk. Existing community plugins (Media Remove,
Unused Media Dashboard) exist but **lag even on test databases** — the real problem is that
they scan everything synchronously and delete hard, with no batching, progress, or safety net.

Two cleanup needs already validated in practice (done previously as throwaway controllers):
1. Delete/move Umbraco media with **zero content relations**.
2. Delete **physical files with no corresponding Media node**.

This document is a **curated, prioritized catalog** of functions worth building into a proper,
reusable extension. Target: **Umbraco v14+** (new Lit/Web-Component backoffice + Management API +
C# backend). No implementation phases here — this is the feature-scoping deliverable to decide
what goes into v1.

---

## Design principles (why this beats the existing plugins)
- **Never hard-delete by default.** Every destructive action is: *dry-run/preview → quarantine
  (move to hidden folder) → grace period → purge*. Reversible until the last step.
- **Async, batched, cancellable.** All scans/actions run as background jobs with progress and
  cancellation — never a single synchronous request. This is the fix for the "lags on test DB" issue.
- **No false orphans.** Relation tracking alone is unreliable; pair it with a deep reference scan
  (see below) before flagging anything as unused.
- **Audit everything.** Who did what, when, and how to undo it.

---

## A. Safe cleanup core *(highest priority)*

| Function | Notes / trade-offs |
|---|---|
| **Orphaned media (zero relations)** | The validated case. Flag → quarantine → purge. Must combine with deep reference scan to avoid false positives. |
| **Orphaned physical files** | File on disk, no Media node. The second validated case. Report size reclaimable. |
| **Broken Media nodes** | Reverse case — Media node exists but the file is missing on disk. Report-only + optional cleanup. |
| **Duplicate detection (file hash)** | Byte-identical files uploaded multiple times. Group by SHA-256; keep one, report/repoint the rest. Hash lazily + cache to stay fast. |
| **Empty folder cleanup** | Media folders with no descendants. |
| **⭐ Deep reference scan** | **The critical safety net.** Umbraco relations miss media referenced only inside rich-text HTML, Markdown, Block List/Grid JSON, nested content, and custom stored values. Scan content property values for the media GUID/UDI/path before flagging as orphan. Without this, "cleanup" deletes in-use media. |

**Cross-cutting for this section:** dry-run preview list, quarantine-move instead of delete,
CSV export of every candidate set, batch size + progress + cancel.

## B. Reporting & dashboard *(high priority)*

| Function | Notes |
|---|---|
| **Storage report** | Total library size, breakdown by media type and by folder, top-N largest files. Answers "what's eating disk". |
| **Accessibility audit** | Images missing alt text — surfaces an SEO/a11y debt list. |
| **Oversized-image detection** | Images beyond a configurable dimension or byte threshold; feeds the optimization section. |
| **Reclaimable-space summary** | Headline number: how much the orphan/duplicate/broken sets would free. |

## C. Scheduling & audit *(high priority)*

| Function | Notes |
|---|---|
| **Recurring background jobs** | `IHostedService` / recurring hosted service (Umbraco `RecurringHostedServiceBase`) to run scans on a schedule instead of one-off controllers. |
| **Audit log** | Every move/delete/purge: item, action, user, timestamp; drives undo and accountability. |
| **Exclusion rules** | Protect chosen folders/types/tags from ever being flagged (e.g. brand assets, legal docs). |
| **Quarantine grace-period purge** | Auto-purge quarantined items after N days unless restored. |

## D. Image optimization *(lower priority — separable module)*

| Function | Notes |
|---|---|
| **Bulk re-compression** | Recompress existing images at a target quality; report bytes saved. |
| **WebP/AVIF conversion** | Convert legacy JPEG/PNG to modern formats. Decide: replace vs. keep original + add variant. |

> Optimization mutates original assets, so it carries the most risk — keep it behind its own
> toggle, always back up / keep originals, and ship it after A–C are stable.

---

## Recommended v1 cut
Ship **A (safe cleanup core) + the shared safety layer** first, with **B's storage/reclaimable
report** and **C's audit log + scheduling** as the supporting frame. Defer **D** to a later,
opt-in module. The deep reference scan and the dry-run→quarantine→purge flow are non-negotiable —
they are what make this trustworthy where the existing plugins aren't.

## Open questions for the build phase (not decided here)
- Storage providers to support: local disk only, or also Azure Blob / S3 (affects orphaned-file scanning).
- Quarantine mechanism: dedicated hidden Media folder vs. a separate store.
- Multi-site / multiple media roots handling.
- Permissions: which backoffice user groups may run destructive actions.
