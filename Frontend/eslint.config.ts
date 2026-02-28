import pluginVitest from "@vitest/eslint-plugin";
import { defineConfigWithVueTs } from "@vue/eslint-config-typescript";
import pluginPlaywright from "eslint-plugin-playwright";

export default defineConfigWithVueTs(
  {
    ignores: ["**/*.vue", "**/*.ts", "**/*.tsx"],
  },
  {
    ...pluginVitest.configs.recommended,
    files: ["src/**/__tests__/*"],
  },
  {
    ...pluginPlaywright.configs["flat/recommended"],
    files: ["e2e/**/*.{test,spec}.{js,ts,jsx,tsx}"],
  },
);
