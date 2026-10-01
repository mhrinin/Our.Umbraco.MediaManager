import type { UmbEntryPointOnInit } from "@umbraco-cms/backoffice/extension-api";

const onInit: UmbEntryPointOnInit = async (_host, extensionRegistry) => {
  let section = "Umb.Section.Settings";

  try {
    const response = await fetch("/umbraco/media-manager/api/v1/config");
    if (response.ok) {
      const data = await response.json();
      if (data?.section) {
        section = data.section;
      }
    }
  } catch {
    // Fall back to default section
  }

  extensionRegistry.register({
    type: "dashboard",
    alias: "Our.Umbraco.MediaManager.Dashboard",
    name: "Media Manager Dashboard",
    element: () =>
      import("./components/dashboards/media-manager-dashboard.element.js"),
    elementName: "media-manager-dashboard",
    weight: 10,
    meta: {
      label: "Media Manager",
      pathname: "media-manager",
    },
    conditions: [
      {
        alias: "Umb.Condition.SectionAlias",
        match: section,
      },
    ],
  });
};

export const manifests: Array<UmbExtensionManifest> = [
  {
    type: "backofficeEntryPoint",
    alias: "Our.Umbraco.MediaManager.EntryPoint",
    name: "Media Manager Entry Point",
    js: () => Promise.resolve({ onInit }),
  },
];

