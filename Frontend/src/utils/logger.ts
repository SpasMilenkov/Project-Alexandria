// oxlint-disable no-console -- this file is the approved console wrapper
const isDev = import.meta.env.DEV;

export const logger = {
  debug: (...args: unknown[]) => isDev && console.debug(...args),
  error: (...args: unknown[]) => isDev && console.error(...args),
  log: (...args: unknown[]) => isDev && console.log(...args),
  warn: (...args: unknown[]) => isDev && console.warn(...args),
};
