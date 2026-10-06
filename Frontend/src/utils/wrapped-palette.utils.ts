export type WrappedPalette = [string, string, string];

export const wrappedPalettePresets: { name: string; colors: WrappedPalette }[] = [
  { name: "Ocean", colors: ["#286789", "#4cced4", "#ffe3b1"] },
  { name: "Afterglow", colors: ["#a34c72", "#ef9671", "#f4d68e"] },
  { name: "Iris", colors: ["#6554a4", "#b19cdb", "#87c8cb"] },
  { name: "Moss", colors: ["#496854", "#94ad79", "#d9c397"] },
];

const inkSlots = {
  blue: 0,
  purple: 1,
  leaf: 2,
  gold: 2,
  sage: 0,
  clay: 1,
  teal: 1,
  sky: 0,
  olive: 2,
  rose: 2,
  lilac: 1,
  sand: 0,
} as const;

export const parseWrappedPalette = (value: unknown): WrappedPalette | null => {
  if (!Array.isArray(value) || value.length !== 3) return null;

  if (!value.every((color) => typeof color === "string" && /^#[\da-f]{6}$/iu.test(color)))
    return null;

  return value.map((color: string) => color.toLowerCase()) as WrappedPalette;
};

const channels = (hex: string): number[] =>
  [1, 3, 5].map((offset) => parseInt(hex.slice(offset, offset + 2), 16));

export const mixWrappedColor = (first: string, second: string, amount: number): string => {
  const other = channels(second);

  return `#${channels(first)
    .map((channel, index) =>
      Math.round(channel + (other[index]! - channel) * amount)
        .toString(16)
        .padStart(2, "0"),
    )
    .join("")}`;
};

const luminance = (hex: string): number => {
  const linear = channels(hex).map((channel) => {
    const value = channel / 255;

    if (value <= 0.04045) return value / 12.92;

    return ((value + 0.055) / 1.055) ** 2.4;
  });

  return linear[0]! * 0.2126 + linear[1]! * 0.7152 + linear[2]! * 0.0722;
};

const contrast = (first: string, second: string): number => {
  const a = luminance(first);
  const b = luminance(second);

  return (Math.max(a, b) + 0.05) / (Math.min(a, b) + 0.05);
};

const readableInk = (color: string, isDark: boolean): string => {
  const paper = isDark ? "#171717" : "#f5f4f0";
  const target = isDark ? "#ffffff" : "#000000";

  for (let step = 0; step <= 100; step++) {
    const candidate = mixWrappedColor(color, target, step / 100);

    if (contrast(candidate, paper) >= 4.5) return candidate;
  }

  return target;
};

export const wrappedPaletteInks = (palette: WrappedPalette | null, isDark: boolean) => {
  if (!palette) return {};

  const inks = palette.map((color) => readableInk(color, isDark));

  return Object.fromEntries(
    Object.entries(inkSlots).map(([key, slot]) => [key, inks[slot]!]),
  ) as Partial<Record<keyof typeof inkSlots, string>>;
};

export const tintWrappedSkyColor = (original: string, selected: string): string => {
  const target = luminance(original);
  const tint = mixWrappedColor(original, selected, 0.65);
  const lighten = luminance(tint) <= target;
  const end = lighten ? "#ffffff" : "#000000";
  let low = 0;
  let high = 1;

  for (let step = 0; step < 16; step++) {
    const amount = (low + high) / 2;
    const value = luminance(mixWrappedColor(tint, end, amount));

    if (value < target === lighten) low = amount;
    else high = amount;
  }

  return mixWrappedColor(tint, end, (low + high) / 2);
};

const lightColors = {
  background: "#f4f1e9",
  panel: "#fffcf6",
  text: "#252936",
  muted: "#59616c",
  line: "#d2d1cc",
  blue: "#245a91",
  purple: "#69478d",
  leaf: "#466330",
  gold: "#746020",
  sage: "#3c6652",
  clay: "#8a4d3c",
  teal: "#24685c",
  sky: "#315d8e",
  olive: "#626b28",
  rose: "#934650",
  lilac: "#67518b",
  sand: "#75603d",
};

const darkColors = {
  ...lightColors,
  background: "#13171e",
  panel: "#1d232c",
  text: "#f1eee7",
  muted: "#b5bcc8",
  line: "#414957",
  blue: "#8ebfff",
  purple: "#b9a2ef",
  leaf: "#d8ecc4",
  gold: "#e8dca4",
  sage: "#9cbfab",
  clay: "#ecb9ad",
  teal: "#a3dacb",
  sky: "#85b3e2",
  olive: "#e2e9b0",
  rose: "#e9afa9",
  lilac: "#b6a6df",
  sand: "#e8d5ad",
};

export const wrappedExportColors = (isDark: boolean, palette: WrappedPalette | null = null) => ({
  ...(isDark ? darkColors : lightColors),
  ...wrappedPaletteInks(palette, isDark),
});

export const wrappedCssColors = (isDark: boolean, palette: WrappedPalette | null = null) => {
  const colors = wrappedExportColors(isDark, palette);

  const neutrals = {
    line: isDark ? "#626677" : "#979ba7",
    track: isDark ? "#343746" : "#d5d8df",
    cutout: isDark ? "#171717" : "#f5f4f0",
    pin: isDark ? "#f2eee6" : "#252936",
  };

  return Object.fromEntries(
    Object.entries({ ...colors, ...neutrals }).map(([key, color]) => [`--wrapped-${key}`, color]),
  );
};
