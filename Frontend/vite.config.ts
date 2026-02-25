import { fileURLToPath, URL } from "node:url";
import { defineConfig } from "vite";
import vue from "@vitejs/plugin-vue";
import vueDevTools from "vite-plugin-vue-devtools";
import ui from "@nuxt/ui/vite";
import { VitePWA } from "vite-plugin-pwa";

// https://vite.dev/config/
export default defineConfig({
  plugins: [
    vue(),
    vueDevTools(),
    ui({
      ui: {
        colors: {
          primary: "orange",
          secondary: "orange-700",
          neutral: "neutral",
        },
        container: {
          base: "w-full max-w-(--ui-container) mx-auto px-4 sm:px-6 lg:px-8",
        },
      },
    }),
    VitePWA({
      strategies: "injectManifest",
      srcDir: "src",
      filename: "bg-image-sw.ts",
      injectManifest: {
        injectionPoint: undefined,
      },
      devOptions: {
        enabled: true,
        type: "module",
      },
    }),
  ],
  resolve: {
    alias: {
      "@": fileURLToPath(new URL("./src", import.meta.url)),
    },
  },
});
