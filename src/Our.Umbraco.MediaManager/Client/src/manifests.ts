export const manifests: Array<UmbExtensionManifest> = [
  {
    type: "backofficeEntryPoint",
    alias: "Our.Umbraco.MediaManager.EntryPoint",
    name: "Media Manager Entry Point",
    js: () => import("./entry-point.js"),
  },
];
