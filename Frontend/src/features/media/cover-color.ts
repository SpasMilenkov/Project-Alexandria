import { dominantColorOf, parseHex, toHex } from "@/utils/color";

const SAMPLE_SIZE = 32;

// Most common coarse color of a loaded image. Null when the canvas is unavailable or tainted.
export const sampleCoverColor = (image: HTMLImageElement): string | null => {
  try {
    const canvas = document.createElement("canvas");
    canvas.width = SAMPLE_SIZE;
    canvas.height = SAMPLE_SIZE;

    const context = canvas.getContext("2d", { willReadFrequently: true });
    if (!context) return null;

    context.drawImage(image, 0, 0, SAMPLE_SIZE, SAMPLE_SIZE);

    return dominantColorOf(context.getImageData(0, 0, SAMPLE_SIZE, SAMPLE_SIZE).data);
  } catch {
    return null;
  }
};

// Pulls the tint toward black (dark) or white (light) first, so solid text tokens keep AA contrast.
export const heroTint = (hex: string, isDark: boolean): string => {
  const target = isDark ? 0 : 255;
  const mixed = parseHex(hex).map((channel) => Math.round(channel * 0.5 + target * 0.5));

  return toHex(mixed);
};

const ACHROMATIC_SATURATION = 0.12;
const MIN_INK_SATURATION = 0.65;
const INK_LIGHTNESS = { dark: 0.68, light: 0.42 };

const toHsl = (hex: string): [number, number, number] => {
  const [r, g, b] = parseHex(hex).map((channel) => channel / 255) as [number, number, number];
  const max = Math.max(r, g, b);
  const min = Math.min(r, g, b);
  const lightness = (max + min) / 2;
  const delta = max - min;

  if (delta === 0) return [0, 0, lightness];

  const saturation = delta / (1 - Math.abs(2 * lightness - 1));
  let hue = (r - g) / delta + 4;

  if (max === r) hue = ((g - b) / delta + 6) % 6;
  else if (max === g) hue = (b - r) / delta + 2;

  return [hue * 60, saturation, lightness];
};

const fromHsl = (hue: number, saturation: number, lightness: number): string => {
  const amplitude = saturation * Math.min(lightness, 1 - lightness);

  const channel = (offset: number) => {
    const k = (offset + hue / 30) % 12;
    const value = lightness - amplitude * Math.max(-1, Math.min(k - 3, 9 - k, 1));

    return Math.round(value * 255);
  };

  return toHex([channel(0), channel(8), channel(4)]);
};

// A chart mark that never reads as gray on glass: the cover's hue at a guaranteed saturation and a
// fixed per-theme lightness that keeps graphic contrast. Covers without a usable hue (black, white,
// grayscale) borrow the hue of the fallback color instead.
export const coverInk = (hex: string, isDark: boolean, fallbackHex: string): string => {
  const [hue, saturation] = toHsl(hex);
  const [fallbackHue] = toHsl(fallbackHex);
  const resolvedHue = saturation < ACHROMATIC_SATURATION ? fallbackHue : hue;
  const lightness = isDark ? INK_LIGHTNESS.dark : INK_LIGHTNESS.light;

  return fromHsl(resolvedHue, Math.max(saturation, MIN_INK_SATURATION), lightness);
};
