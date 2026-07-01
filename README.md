# Ideo.Umbraco.MediaManager

A safe, async media-cleanup extension for **Umbraco v17** (backoffice v14+ / Management API).

Find and remove media that no longer belongs in your library — **orphaned media nodes** (no
content references anywhere) and **orphaned physical files** (files on disk with no media node) —
without the risk that makes existing "delete unused media" plugins dangerous.

## Why it is safe

- **Deep reference scan** — before flagging a media item as orphan, its GUID/UDI is searched
  across all content property values (rich text, Block List/Grid, nested content), **published
  *and* draft**. Umbraco relations alone miss these; this scan is the guard against false orphans.
- **Mandatory dry-run preview** — you always see the exact set that would change before anything
  happens.
- **Recycle Bin, not hard delete** — orphaned media nodes are moved to Umbraco's Media Recycle
  Bin, so they are fully recoverable until you explicitly empty the bin. Orphaned physical files
  (which have no node, and so no bin) are deleted only via an explicit, previewed action.
- **Async & cancellable** — scans run as background jobs with progress, so they never lock up on
  large libraries.

## Requirements

- Umbraco CMS `17.x` (.NET 10)

## Installation

```bash
dotnet add package Ideo.Umbraco.MediaManager
```

The MediaManager dashboard appears in the backoffice **Media** section.

## Local development

```bash
# Frontend (Lit + Vite)
cd src/Ideo.Umbraco.MediaManager/Client
npm ci
npm run build          # emits to ../wwwroot/App_Plugins/MediaManager

# Run the sample host (Umbraco 17, SQLite, unattended install)
cd ../../../samples/Ideo.Umbraco.MediaManager.Web
dotnet run
```

## Repository layout

```
src/Ideo.Umbraco.MediaManager      # the package (C# backend + Client/ frontend)
samples/…Web                       # local Umbraco 17 host for dev + manual testing
tests/…Tests                       # unit tests
docs/                              # feature proposal / design notes
```

## Releasing

Push a `v*` git tag (e.g. `v0.1.0`); the release workflow packs and publishes to nuget.org using
**Trusted Publishing** (OIDC) — no stored API key. Configure a Trusted Publishing policy at
nuget.org (Repository Owner `mhrinin`, Repository `Ideo.Umbraco.MediaManager`, Workflow File
`release.yml`) and set the `NUGET_USER` repository variable to your nuget.org username.

## License

[MIT](LICENSE)
