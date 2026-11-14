import { logger } from "@/utils/logger";

// Chrome, Firefox and Safari each word a failed dynamic import differently.
const STALE_CHUNK_PATTERNS = [
  /failed to fetch dynamically imported module/iu,
  /error loading dynamically imported module/iu,
  /importing a module script failed/iu,
  /unable to preload css/iu,
];

const RELOAD_STAMP_KEY = "alexandria:stale-chunk-reload";

// Failing again this soon after a reload means the files are missing, not stale, so reloading
// once more would only loop.
const RELOAD_COOLDOWN_MS = 10_000;

export const isStaleChunkError = (error: unknown): boolean => {
  if (!(error instanceof Error)) return false;

  return STALE_CHUNK_PATTERNS.some((pattern) => pattern.test(error.message));
};

const readLastReload = (): number | undefined => {
  try {
    return Number(sessionStorage.getItem(RELOAD_STAMP_KEY) ?? 0);
  } catch (error) {
    logger.warn(
      "Session storage is unavailable, so stale pages will not reload on their own",
      error,
    );
    return undefined;
  }
};

const writeReloadStamp = (now: number): boolean => {
  try {
    sessionStorage.setItem(RELOAD_STAMP_KEY, String(now));
    return true;
  } catch (error) {
    logger.warn("Could not record the stale page reload", error);
    return false;
  }
};

interface StaleChunkReloadOptions {
  navigate?: (href: string) => void;
  now?: number;
}

const navigateTo = (href: string) => {
  window.location.assign(href);
};

/**
 * Reloads into `targetHref` after a deploy removed the chunks this tab was built against.
 * Returns false when a reload already happened moments ago, or when the guard against a
 * reload loop cannot be stored.
 */
export const reloadForStaleChunk = (
  targetHref: string,
  { navigate = navigateTo, now = Date.now() }: StaleChunkReloadOptions = {},
): boolean => {
  const lastReload = readLastReload();

  if (lastReload === undefined) return false;
  if (now - lastReload < RELOAD_COOLDOWN_MS) return false;
  if (!writeReloadStamp(now)) return false;

  navigate(targetHref);
  return true;
};
