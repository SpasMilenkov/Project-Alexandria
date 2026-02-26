import ui from "@nuxt/ui/vite";
import vue from "@vitejs/plugin-vue";
import { URL, fileURLToPath } from "node:url";
import { defineConfig } from "vite";
import { VitePWA } from "vite-plugin-pwa";
import vueDevTools from "vite-plugin-vue-devtools";

// https://vite.dev/config/
export default defineConfig({
  plugins: [
    vue(),
    vueDevTools(),
    ui({
      ui: {
        colors: {
          neutral: "neutral",
          primary: "orange",
          secondary: "orange-700",
        },
        container: {
          base: "w-full max-w-(--ui-container) mx-auto px-4 sm:px-6 lg:px-8",
        },
      },
    }),
    VitePWA({
      devOptions: {
        enabled: true,
        type: "module",
      },
      filename: "bg-image-sw.ts",
      injectManifest: {
        injectionPoint: undefined,
      },
      srcDir: "src",
      strategies: "injectManifest",
    }),
  ],
  resolve: {
    alias: {
      "@": fileURLToPath(new URL("./src", import.meta.url)),
    },
  },
});
