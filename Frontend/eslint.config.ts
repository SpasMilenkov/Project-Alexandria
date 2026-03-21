import pluginVitest from "@vitest/eslint-plugin";
import pluginPlaywright from "eslint-plugin-playwright";

export default [
  {
    ...pluginVitest.configs.recommended,
    files: ["src/**/__tests__/**/*"],
    lint: "eslint --no-warn-ignored",
  },
  {
    ...pluginPlaywright.configs["flat/recommended"],
    files: ["e2e/**/*.{test,spec}.{js,ts,jsx,tsx}"],
  },
];
