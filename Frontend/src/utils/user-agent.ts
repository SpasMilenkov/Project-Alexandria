// A readable "Firefox on Linux" from a user agent string. Order matters: Edge and Opera
// also claim Chrome, and Chrome also claims Safari.
const BROWSERS: [RegExp, string][] = [
  [/Edg(?:e|A|iOS)?\//u, "Edge"],
  [/OPR\/|Opera/u, "Opera"],
  [/Firefox\/|FxiOS\//u, "Firefox"],
  [/SamsungBrowser\//u, "Samsung Internet"],
  [/Chrome\/|CriOS\//u, "Chrome"],
  [/Safari\//u, "Safari"],
];

const SYSTEMS: [RegExp, string][] = [
  [/Android/u, "Android"],
  [/iPhone|iPad|iPod/u, "iOS"],
  [/Windows/u, "Windows"],
  [/Mac OS X|Macintosh/u, "macOS"],
  [/CrOS/u, "ChromeOS"],
  [/Linux/u, "Linux"],
];

const MOBILE = /Mobile|Android|iPhone|iPad|iPod/u;

const match = (value: string, table: [RegExp, string][]): string | null =>
  table.find(([pattern]) => pattern.test(value))?.[1] ?? null;

export interface UserAgentSummary {
  label: string;
  isMobile: boolean;
}

export const describeUserAgent = (userAgent: string | null | undefined): UserAgentSummary => {
  if (!userAgent?.trim()) return { isMobile: false, label: "Unknown device" };

  const browser = match(userAgent, BROWSERS);
  const system = match(userAgent, SYSTEMS);
  const isMobile = MOBILE.test(userAgent);

  if (browser && system) return { isMobile, label: `${browser} on ${system}` };
  if (browser) return { isMobile, label: browser };
  if (system) return { isMobile, label: system };

  return { isMobile, label: "Unknown browser" };
};
