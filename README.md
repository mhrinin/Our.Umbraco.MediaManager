# Our.Umbraco.MediaManager

Safe, async media cleanup for Umbraco — find and remove media that no longer belongs in your
library, without ever deleting anything that's still in use.

Built for the new backoffice (**Umbraco 16 & 17**), with maintained packages for **13 (LTS)**
and **10**.

![Media Manager dashboard](https://raw.githubusercontent.com/mhrinin/Our.Umbraco.MediaManager/main/docs/screenshots/01-overview-unused.png)

## What it finds

- **Unused media** — nodes with no content references anywhere
- **Duplicates** — byte-identical files
- **Broken media** — nodes whose underlying file is missing
- **Orphaned files** — files on disk with no media node
- **Storage report** — totals, breakdown by type, and largest files

## Safe by design

Existing "delete unused media" tools are risky because they trust relations alone and hard-delete.
This one doesn't:

- **Deep reference scan** — a media item's GUID/UDI is searched across every content property value
  (rich text, Block List/Grid, nested content), **published *and* draft**, on top of Umbraco
  relations — before it is ever flagged as unused. This is the guard against false positives.
- **Preview first** — every action shows the exact set that will change before it runs.
- **Recycle Bin, not hard delete** — media nodes are moved to the Media Recycle Bin, recoverable
  until you empty it.
- **Validated file deletion** — orphaned physical files are removed only through an explicit,
  previewed action, and the server accepts only paths its own scan flagged.
- **Permission-aware** — honours each user's media start nodes and per-node permissions, exactly
  like Umbraco's own endpoints.
- **Async & cancellable** — scans run as background jobs with progress, so large libraries never
  lock up the UI.

## Requirements

| Package | Umbraco | .NET |
| --- | --- | --- |
| `17.x` | 16 or 17 | 9 / 10 |
| `13.x` | 13 (LTS) | 8 |
| `10.x` | 10 | 6 |

The `17.x` package multi-targets — an Umbraco 16 site gets the net9.0 build, an Umbraco 17 site the
net10.0 one.

## Install

```bash
dotnet add package Our.Umbraco.MediaManager                # Umbraco 16 / 17
dotnet add package Our.Umbraco.MediaManager --version 13.* # Umbraco 13
dotnet add package Our.Umbraco.MediaManager --version 10.* # Umbraco 10
```

Then open **Settings → Media Manager** in the backoffice (Settings section access required) and run
a scan.

## Configuration

```json
{
  "MediaManager": {
    "DeepReferenceScan": true,
    "Section": "Settings"
  }
}
```

- `DeepReferenceScan` (default `true`) also scans content property values — published and draft — on
top of Umbraco relations. Turn it off on very large sites to rely on relations only.
- `Section` (default `"Settings"`) controls which backoffice section hosts the dashboard and whose
access policy guards the API. Allowed values are `"Settings"` and `"Media"`; any other value fails at
startup. With `"Media"`, editors with Media section access can use the dashboard without Settings
access. Note that scan results then cover the whole media library regardless of the user's media start
nodes; deleting media still honours start nodes and per-node permissions, and deleting orphaned files
always requires Settings section access.

## Contributing

```bash
# Frontend (Lit + Vite)
cd src/Our.Umbraco.MediaManager/Client && npm ci && npm run build

# Sample host (SQLite, unattended install)
cd samples/Our.Umbraco.MediaManager.Web && dotnet run
```

## License

[MIT](LICENSE)
