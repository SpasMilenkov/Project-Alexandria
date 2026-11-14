// Vue names the failing phase in development and sends an error-reference URL in production.
const PAGE_BREAKING_PHASES = new Set([
  "setup function",
  "render function",
  "async component loader",
  "https://vuejs.org/error-reference/#runtime-0",
  "https://vuejs.org/error-reference/#runtime-1",
  "https://vuejs.org/error-reference/#runtime-13",
]);

// A failing event handler, watcher or hook leaves the page usable, so only these failures
// replace it with the error screen.
export const isPageBreakingError = (info: string): boolean => PAGE_BREAKING_PHASES.has(info);
