import { mulberry32 } from "@/utils/random";

const palettes = [
  ["#e4572e", "#f3a712", "#29335c", "#f5efe0"],
  ["#ef476f", "#ffd166", "#118ab2", "#fff8ec"],
  ["#ff6b6b", "#4ecdc4", "#2b2d42", "#f7f1e3"],
  ["#d1495b", "#edae49", "#00798c", "#f6efe4"],
  ["#7b2cbf", "#ff9e00", "#14213d", "#f4ecdd"],
] as const;

export const homeDailyCover = (day: string, offset = 0) => {
  const seed = Number(day.replace(/-/gu, "")) + offset;
  const random = mulberry32(seed);
  const palette = palettes[seed % palettes.length]!;
  const ink = () => palette[Math.floor(random() * 3)]!;
  const circles = Array.from({ length: 6 }, () => ({
    x: random() * 200,
    y: random() * 200,
    radius: 25 + random() * 50,
    fill: ink(),
  }));
  const halftone = { x: 60 + random() * 80, y: 60 + random() * 80, radius: 40 + random() * 30 };
  const cells = Array.from({ length: 16 }, (_, index) => ({
    x: (index % 4) * 50,
    y: Math.floor(index / 4) * 50,
    fill: ink(),
    kind: Math.floor(random() * 5),
  }));
  const sun = { x: 50 + random() * 100, y: 70 + random() * 20, radius: 18 + random() * 12 };
  const ridges = Array.from({ length: 5 }, (_, index) => {
    const base = 100 + index * 22;
    const points = Array.from(
      { length: 11 },
      (_point, x) => `L${x * 20} ${base - random() * 25}`,
    ).join(" ");

    return { path: `M0 200 ${points} L200 200Z`, opacity: 0.2 + index * 0.17 };
  });

  return { seed, palette, circles, halftone, cells, sun, ridges };
};
