/** Reads a `#rrggbb` (or `rrggbb`) color into its three 0-255 channels. */
export const parseHex = (hex: string): number[] => {
  const value = hex.replace("#", "");

  return [0, 2, 4].map((offset) => parseInt(value.slice(offset, offset + 2), 16));
};

/** Formats 0-255 channels as a lowercase `#rrggbb` string. */
export const toHex = (channels: number[]): string =>
  `#${channels.map((channel) => channel.toString(16).padStart(2, "0")).join("")}`;

/**
 * Most common coarse color of RGBA pixel data: channels snap to steps of 32 and mostly
 * transparent pixels are skipped. Ties go to the color seen first. Null when nothing is visible.
 */
export const dominantColorOf = (data: Uint8ClampedArray): string | null => {
  const counts = new Map<string, number>();

  for (let index = 0; index < data.length; index += 4) {
    if (data[index + 3]! >= 128) {
      const key = [0, 1, 2]
        .map((offset) => Math.min(255, Math.round(data[index + offset]! / 32) * 32))
        .join(",");
      counts.set(key, (counts.get(key) ?? 0) + 1);
    }
  }

  let dominant: string | null = null;
  let best = 0;

  for (const [key, count] of counts) {
    if (count > best) {
      dominant = key;
      best = count;
    }
  }

  if (!dominant) return null;

  return toHex(dominant.split(",").map(Number));
};
