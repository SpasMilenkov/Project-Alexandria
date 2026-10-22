import { fileURLToPath } from "node:url";
import type { ConfigEnv } from "vite";
import { configDefaults, defineConfig, mergeConfig } from "vitest/config";

import viteConfig from "./vite.config";

const configEnv: ConfigEnv = {
  command: "serve",
  isPreview: false,
  isSsrBuild: false,
  mode: "test",
};

export default mergeConfig(
  viteConfig(configEnv),
  defineConfig({
    test: {
      environment: "jsdom",
      pool: "vmThreads",
      exclude: [...configDefaults.exclude, "e2e/**"],
      root: fileURLToPath(new URL("./", import.meta.url)),
      setupFiles: ["./vitest.setup.ts"],
    },
  }),
);
