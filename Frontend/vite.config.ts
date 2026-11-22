import ui from "@nuxt/ui/vite";
import { PiniaColadaDevtools } from "@pinia/colada-devtools/vite";
import { serwist } from "@serwist/vite";
import { DevTools } from "@vitejs/devtools";
import vue from "@vitejs/plugin-vue";
import { URL, fileURLToPath } from "node:url";
import { defineConfig, loadEnv } from "vite";
import vueDevTools from "vite-plugin-vue-devtools";

export default defineConfig(({ mode }) => {
  const env = loadEnv(mode, process.cwd(), "VITE_");

  return {
    define: {
      __ALEXANDRIA_API_BASE_URL__: JSON.stringify(env.VITE_API_BASE_URL ?? ""),
    },
    plugins: [
      vue(),
      vueDevTools(),
      DevTools(),
      PiniaColadaDevtools(),
      ui({
        // Only the glyphs named in source are bundled. The CSP blocks the Iconify API, so a
        // glyph the scan misses renders blank instead of loading at runtime.
        icon: {
          clientBundle: {
            icons: [],
            scan: {
              globExclude: ["node_modules", "dist", "src/__tests__/**", "e2e/**"],
              // The favicon glyphs and most menu icons are named in .ts files.
              globInclude: ["src/**/*.{vue,ts}"],
              // Collections match longest first, so "i-mdi-light..." would scan as mdi-light.
              ignoreCollections: ["mdi-light"],
            },
          },
        },
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
    // Nuxt UI's editor loads Tiptap and ProseMirror unbundled in dev. Pre-bundling the Tiptap
    // packages the notes code imports directly would give it a second ProseMirror, and two
    // copies of its plugin registry collide as soon as the editor mounts.
    optimizeDeps: {
      exclude: ["@tiptap/core", "@tiptap/vue-3"],
    },
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
