import type { UmbEntryPointOnInit, UmbEntryPointOnUnload } from "@umbraco-cms/backoffice/extension-api";
import { MediaManagerRepository } from "./services/media-manager.repository.js";

const DASHBOARD_ALIAS = "Our.Umbraco.MediaManager.Dashboard";
const DEFAULT_SECTION = "Umb.Section.Settings";

export const onInit: UmbEntryPointOnInit = async (host, extensionRegistry) => {
  const configuration = await new MediaManagerRepository(host).getConfiguration();

  const dashboard: UmbExtensionManifest = {
    type: "dashboard",
    alias: DASHBOARD_ALIAS,
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
        match: configuration?.section ?? DEFAULT_SECTION,
      },
    ],
  };

  extensionRegistry.register(dashboard);
};

export const onUnload: UmbEntryPointOnUnload = (_host, extensionRegistry) => {
  extensionRegistry.unregister(DASHBOARD_ALIAS);
};
