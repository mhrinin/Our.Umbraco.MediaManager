import { defineConfig } from "vite";

export default defineConfig({
  build: {
    lib: {
      entry: "src/components/dashboards/media-manager-dashboard.element.ts",
      formats: ["es"],
      fileName: "media-manager",
    },
    outDir: "../wwwroot/App_Plugins/MediaManager",
    emptyOutDir: true,
    sourcemap: true,
    rollupOptions: {
      external: [/^@umbraco/],
    },
  },
});
