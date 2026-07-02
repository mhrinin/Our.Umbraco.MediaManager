# Ideo.Umbraco.MediaManager

A safe, async media-cleanup extension for **Umbraco 16 & 17** (new backoffice / Management API).
For **Umbraco 13 (LTS)** use the `13.x` package versions (`v13/main` branch); for **Umbraco 10**
use `10.x` (`v10/main` branch).

Find and remove media that no longer belongs in your library — **unused media nodes** (no content
references anywhere), **duplicates** (byte-identical files), **broken media** (nodes whose file is
missing) and **orphaned physical files** (files on disk with no media node) — plus a **storage
report** (totals, breakdown by type, largest files) — without the risk that makes existing
"delete unused media" plugins dangerous.

## Why it is safe

- **Deep reference scan** — before flagging a media item as unused, its GUID/UDI is searched
  across all content property values (rich text, Block List/Grid, nested content), **published
  *and* draft**, on top of Umbraco relations. This is the guard against false positives.
- **Mandatory preview** — you always see the exact set that would change before anything happens.
- **Recycle Bin, not hard delete** — unused/duplicate/broken media nodes are moved to Umbraco's
  Media Recycle Bin, so they are fully recoverable until you explicitly empty the bin.
- **Server-validated file deletion** — orphaned physical files (which have no node, and so no
  bin) are deleted only via an explicit, previewed action that requires Settings section access,
  and the server only accepts paths its own scan actually flagged.
- **Permission-aware** — media deletion honours the user's media start nodes and per-node
  permissions, exactly like Umbraco's own endpoints.
- **Async & cancellable** — scans run as background jobs with progress, so they never lock up on
  large libraries.

## Requirements & versions

| Package version | Umbraco | .NET |
| --- | --- | --- |
| `17.x` (this branch) | 16.x or 17.x | 9 / 10 |
| `13.x` (`v13/main` branch) | 13.x (LTS) | 8 |
| `10.x` (`v10/main` branch) | 10.x | 6 |

The `17.x` package multi-targets: an Umbraco 16 site gets the net9.0 assembly, an Umbraco 17 site
the net10.0 one — same package, correct dependencies either way.

## Installation

```bash
# Umbraco 16 / 17
dotnet add package Ideo.Umbraco.MediaManager

# Umbraco 13
dotnet add package Ideo.Umbraco.MediaManager --version 13.*
```

The Media Manager dashboard appears in the backoffice **Settings** section (Settings section
access is required to use it).

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
# Frontend (Lit + Vite)
cd src/Ideo.Umbraco.MediaManager/Client
npm ci
npm run build          # emits to ../wwwroot/App_Plugins/MediaManager

# Run the sample hosts (SQLite, unattended install)
cd ../../../samples/Ideo.Umbraco.MediaManager.Web        # Umbraco 17
dotnet run
cd ../Ideo.Umbraco.MediaManager.Web.V16                  # Umbraco 16
dotnet run
```

## Repository layout

```
src/Ideo.Umbraco.MediaManager      # the package (C# backend + Client/ frontend)
samples/…Web                       # Umbraco 17 host for dev + manual testing
samples/…Web.V16                   # Umbraco 16 host for cross-version testing
tests/…Tests                       # unit tests (net9.0 + net10.0)
docs/                              # feature proposal / design notes
```

## Releasing

Package versions follow the supported Umbraco major. From this branch, push a `v17.x.y` tag
(e.g. `v17.0.0`); from `v13/main`, push a `v13.x.y` tag. The release workflows pack and publish
to nuget.org using **Trusted Publishing** (OIDC) — no stored API key. Configure a Trusted
Publishing policy at nuget.org (Repository Owner `mhrinin`, Repository
`Ideo.Umbraco.MediaManager`, Workflow File `release.yml`) and set the `NUGET_USER` repository
variable to your nuget.org username.

## License

[MIT](LICENSE)
