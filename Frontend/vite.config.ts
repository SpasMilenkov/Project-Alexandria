import ui from "@nuxt/ui/vite";
import { serwist } from "@serwist/vite";
import vue from "@vitejs/plugin-vue";
import { URL, fileURLToPath } from "node:url";
import { defineConfig, loadEnv } from "vite";
import vueDevTools from "vite-plugin-vue-devtools";
import { PiniaColadaDevtools } from '@pinia/colada-devtools/vite'
import { DevTools } from '@vitejs/devtools'

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
