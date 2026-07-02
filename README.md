# Ideo.Umbraco.MediaManager (Umbraco 13 line)

A safe media-cleanup extension for **Umbraco 13 (LTS)** — this branch (`v13/main`) ships the
legacy-backoffice (AngularJS) edition. For **Umbraco 16/17** use the `main` branch / the `16.x`+
package versions.

Find and remove media that no longer belongs in your library — **unused media nodes** (no content
references anywhere), **duplicates** (byte-identical files), **broken media** (nodes whose file is
missing) and **orphaned physical files** (files on disk with no media node) — without the risk that
makes existing "delete unused media" plugins dangerous.

## Why it is safe

- **Deep reference scan** — before flagging a media item as unused, its GUID/UDI is searched
  across all content property values (rich text, Block List/Grid, nested content), **published
  *and* draft**, on top of Umbraco relations. This is the guard against false positives.
- **Mandatory preview** — you always see the exact set that would change before anything happens.
- **Recycle Bin, not hard delete** — unused/duplicate/broken media nodes are moved to Umbraco's
  Media Recycle Bin, so they are fully recoverable until you explicitly empty the bin. Orphaned
  physical files (which have no node, and so no bin) are deleted only via an explicit, previewed
  action that additionally requires Settings section access.
- **Async & cancellable** — scans run as background jobs with progress, so they never lock up on
  large libraries.

## Requirements

- Umbraco CMS `13.x` (.NET 8)

## Installation

```bash
dotnet add package Ideo.Umbraco.MediaManager --version 13.*
```

The Media Manager dashboard appears in the backoffice **Media** section.

## Configuration

```json
{
  "MediaManager": {
    "DeepReferenceScan": true
  }
}
```

`DeepReferenceScan` (default `true`) additionally scans content property values — published and
draft — for references. Disable on very large sites to fall back to relations only.

## Local development

```bash
# The AngularJS UI is plain source under src/…/wwwroot/App_Plugins/MediaManager — no build step.

# Run the sample host (Umbraco 13, SQLite, unattended install)
cd samples/Ideo.Umbraco.MediaManager.Web
dotnet run
```

## Repository layout (this branch)

```
src/Ideo.Umbraco.MediaManager      # the package (C# backend + AngularJS App_Plugins UI)
samples/…Web                       # local Umbraco 13 host for dev + manual testing
tests/…Tests                       # unit tests
docs/                              # feature proposal / design notes
```

## Releasing

Push a `v13.*` git tag (e.g. `v13.0.1`); the release workflow packs and publishes to nuget.org
using **Trusted Publishing** (OIDC) — no stored API key. The `main` branch owns non-13 tags.

## License

[MIT](LICENSE)
