import ui from "@nuxt/ui/vite";
import { PiniaColadaDevtools } from "@pinia/colada-devtools/vite";
import { serwist } from "@serwist/vite";
import { DevTools } from "@vitejs/devtools";
import vue from "@vitejs/plugin-vue";
import { URL, fileURLToPath } from "node:url";
import { defineConfig, loadEnv } from "vite";
import vueDevTools from "vite-plugin-vue-devtools";

import { iconifySubsetPlugin } from "./plugins/iconify-subset";

export default defineConfig(({ mode }) => {
  const env = loadEnv(mode, process.cwd(), "VITE_");

  return {
    define: {
      __ALEXANDRIA_API_BASE_URL__: JSON.stringify(env.VITE_API_BASE_URL ?? ""),
    },
    plugins: [
      vue(),
      iconifySubsetPlugin(),
      vueDevTools(),
      DevTools(),
      PiniaColadaDevtools(),
      ui({
        ui: {
          colors: {
            neutral: "neutral",
            primary: "orange",
            secondary: "orange-700",
            // Kept apart from the orange accent so a warning never reads as "selected" or "main action".
            warning: "amber",
          },
          container: {
            base: "w-full max-w-(--ui-container) mx-auto px-4 sm:px-6 lg:px-8",
          },
          // Toasts are overlays, so they stay solid. Severity lives in the source tile and the
          // countdown bar, which follows the rounded corners instead of being clipped by them.
          toast: {
            slots: {
              actions: "flex gap-2 shrink-0",
              avatar: "shrink-0 rounded-lg",
              description: "text-sm text-gray-600 dark:text-gray-400 wrap-break-word",
              progress: "absolute inset-x-0 bottom-0",
              root: "relative group overflow-hidden bg-default shadow-lg rounded-xl ring ring-gray-200/70 dark:ring-gray-700/70 py-3 pl-4 pr-3 flex gap-3",
              title: "text-sm font-semibold text-gray-900 dark:text-gray-100",
            },
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
    worker: {
      format: "es",
    },
    server: {
      host: true,
      port: 5173,
      hmr: {
        protocol: "wss",
        host: "localhost",
        clientPort: 8443,
      },
    },
  };
});
