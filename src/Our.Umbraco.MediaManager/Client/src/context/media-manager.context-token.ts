import { UmbContextToken } from "@umbraco-cms/backoffice/context-api";
import type { MediaManagerContext } from "./media-manager.context.js";

export const MEDIA_MANAGER_CONTEXT = new UmbContextToken<MediaManagerContext>(
  "Our.Umbraco.MediaManager.Context",
);
