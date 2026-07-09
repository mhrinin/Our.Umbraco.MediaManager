# Our.Umbraco.MediaManager (Umbraco 10)

Safe media cleanup for **Umbraco 10** — find and remove media that no longer belongs in your
library, without ever deleting anything that's still in use.

This is the legacy-backoffice (AngularJS) edition, maintained on the `v10/main` branch. For
**Umbraco 16 & 17**, use the `main` branch / the `17.x` package.

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
- **Async & cancellable** — scans run as background jobs with progress, so large libraries never
  lock up the UI.

## Requirements

- Umbraco `10.x` · .NET 6

> Umbraco 10 and .NET 6 are end-of-life upstream. This line is a convenience for existing v10
> sites; new features land on the `main` (Umbraco 16/17) line first, and upgrading Umbraco remains
> the recommended path.

## Install

```bash
dotnet add package Our.Umbraco.MediaManager --version 10.*
```

Then open **Settings → Media Manager** in the backoffice and run a scan.

## Configuration

```json
{
  "MediaManager": {
    "DeepReferenceScan": true
  }
}
```

`DeepReferenceScan` (default `true`) also scans content property values — published and draft — on
top of Umbraco relations. Turn it off on very large sites to rely on relations only.

## Contributing

The UI is plain AngularJS under `App_Plugins/MediaManager` — no build step. Run the sample host
(Umbraco 10, SQLite, unattended install):

```bash
cd samples/Our.Umbraco.MediaManager.Web && dotnet run
```

## License

[MIT](LICENSE)
