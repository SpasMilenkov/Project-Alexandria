import ui from "@nuxt/ui/vite";
import { serwist } from "@serwist/vite";
import vue from "@vitejs/plugin-vue";
import { URL, fileURLToPath } from "node:url";
import { defineConfig } from "vite";
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
          warning: "orange",
        },
        container: {
          base: "w-full max-w-(--ui-container) mx-auto px-4 sm:px-6 lg:px-8",
        },
      },
    }),
    serwist({
      devOptions: {
        bundle: true,
      },
      globDirectory: "dist",
      injectionPoint: "self.__SW_MANIFEST",
      rollupFormat: "iife",
      swDest: "sw.js",
      swSrc: "src/sw.ts",
    }),
  ],
  resolve: {
    alias: {
      "@": fileURLToPath(new URL("./src", import.meta.url)),
    },
  },
});
