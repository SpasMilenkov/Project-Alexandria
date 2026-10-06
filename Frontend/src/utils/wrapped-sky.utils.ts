import { WrappedTimeScene } from "@/enums/wrapped-story";
import { type WrappedPalette, tintWrappedSkyColor } from "@/utils/wrapped-palette.utils";

interface SkyPreset {
  eyebrow: string;
  headline: string;
  sub: string;
  skyStops: { offset: string; color: string }[];
  stars: boolean;
  starMaxY?: number;
  starCount?: number;
  seed?: number;
  sunX: number;
  sunY: number;
  sunR: number;
  sunColor: string;
  glowColor: string;
  glowR1: number;
  glowR2: number;
  ridgeBack: string;
  ridgeFront: string;
  baseY: number;
  ridgeGap: number;
  grainColor: string;
  grainOpacity: number;
  scrimOpacity: number;
  seedA: number;
  seedB: number;
  seedC: number;
  seedD: number;
}

// Scene lighting stays independent of the application's light/dark mode.
export const skyPresets: Record<WrappedTimeScene, SkyPreset> = {
  [WrappedTimeScene.Morning]: {
    eyebrow: "Early morning",
    headline: "Early mornings have your sound",
    sub: "Your listening gathers before sunrise. A quiet start.",

    skyStops: [
      { offset: "0%", color: "#93a8d6" },
      { offset: "55%", color: "#e7b7ad" },
      { offset: "100%", color: "#f6d9b8" },
    ],

    stars: false,
    sunX: 640,
    sunY: 190,
    sunR: 34,
    sunColor: "#fbe3a1",
    glowColor: "#fddca0",
    glowR1: 70,
    glowR2: 130,
    ridgeBack: "#8a7f93",
    ridgeFront: "#332b45",
    baseY: 240,
    ridgeGap: 26,
    grainColor: "#5b4a55",
    grainOpacity: 0.05,
    scrimOpacity: 0.55,
    seedA: 1.1,
    seedB: 2.3,
    seedC: 0.4,
    seedD: 3.1,
  },

  [WrappedTimeScene.Midday]: {
    eyebrow: "Midday",
    headline: "Midday has your sound",
    sub: "Your listening peaks around noon, right through lunch.",

    skyStops: [
      { offset: "0%", color: "#3f7dc4" },
      { offset: "60%", color: "#8fc0e6" },
      { offset: "100%", color: "#e9f4fb" },
    ],

    stars: false,
    sunX: 430,
    sunY: 66,
    sunR: 30,
    sunColor: "#fff3b0",
    glowColor: "#fff6c9",
    glowR1: 55,
    glowR2: 95,
    ridgeBack: "#6f9467",
    ridgeFront: "#33502c",
    baseY: 244,
    ridgeGap: 24,
    grainColor: "#2b3a25",
    grainOpacity: 0.045,
    scrimOpacity: 0.48,
    seedA: 2.2,
    seedB: 0.6,
    seedC: 1.4,
    seedD: 2.9,
  },

  [WrappedTimeScene.Afternoon]: {
    eyebrow: "Afternoon",
    headline: "Afternoons have your sound",
    sub: "Your listening gathers in the afternoon. Mostly on weekdays.",

    skyStops: [
      { offset: "0%", color: "#3a6fa8" },
      { offset: "55%", color: "#7fa8c2" },
      { offset: "100%", color: "#f0dba0" },
    ],

    stars: false,
    sunX: 570,
    sunY: 150,
    sunR: 38,
    sunColor: "#f0b84c",
    glowColor: "#f3c766",
    glowR1: 75,
    glowR2: 135,
    ridgeBack: "#7c8a52",
    ridgeFront: "#4b5a2e",
    baseY: 246,
    ridgeGap: 26,
    grainColor: "#3a3620",
    grainOpacity: 0.05,
    scrimOpacity: 0.5,
    seedA: 0.7,
    seedB: 3.3,
    seedC: 2.1,
    seedD: 1.5,
  },

  [WrappedTimeScene.Evening]: {
    eyebrow: "Evening",
    headline: "Evenings have your sound",
    sub: "Your listening picks up after sunset, into the early evening.",

    skyStops: [
      { offset: "0%", color: "#2c2140" },
      { offset: "45%", color: "#79426b" },
      { offset: "80%", color: "#d96b4a" },
      { offset: "100%", color: "#f3a35c" },
    ],

    stars: true,
    starMaxY: 0.28,
    starCount: 28,
    seed: 5,
    sunX: 230,
    sunY: 210,
    sunR: 36,
    sunColor: "#e8703f",
    glowColor: "#e8875a",
    glowR1: 80,
    glowR2: 150,
    ridgeBack: "#4a2f4a",
    ridgeFront: "#241c33",
    baseY: 250,
    ridgeGap: 22,
    grainColor: "#1c1424",
    grainOpacity: 0.06,
    scrimOpacity: 0.42,
    seedA: 1.8,
    seedB: 2.6,
    seedC: 0.9,
    seedD: 3.4,
  },

  [WrappedTimeScene.Night]: {
    eyebrow: "Late night",
    headline: "Late nights have your sound",
    sub: "Your listening holds on well past midnight.",

    skyStops: [
      { offset: "0%", color: "#0d0f1a" },
      { offset: "60%", color: "#14182c" },
      { offset: "100%", color: "#1c2340" },
    ],

    stars: true,
    starMaxY: 0.78,
    starCount: 90,
    seed: 13,
    sunX: 420,
    sunY: 82,
    sunR: 24,
    sunColor: "#e7e9f2",
    glowColor: "#aeb6d9",
    glowR1: 50,
    glowR2: 90,
    ridgeBack: "#1c2033",
    ridgeFront: "#0a0b12",
    baseY: 244,
    ridgeGap: 20,
    grainColor: "#ffffff",
    grainOpacity: 0.035,
    scrimOpacity: 0.5,
    seedA: 2.9,
    seedB: 1.1,
    seedC: 3.7,
    seedD: 0.5,
  },
};

export const getWrappedSkyPreset = (
  scene: WrappedTimeScene,
  palette: WrappedPalette | null = null,
): SkyPreset => {
  const original = skyPresets[scene] ?? skyPresets[WrappedTimeScene.Midday];

  if (!palette) return original;

  return {
    ...original,

    skyStops: original.skyStops.map((stop, index) => ({
      ...stop,
      color: tintWrappedSkyColor(stop.color, palette[index % 3]!),
    })),

    sunColor: tintWrappedSkyColor(original.sunColor, palette[2]),
    glowColor: tintWrappedSkyColor(original.glowColor, palette[2]),
    ridgeBack: tintWrappedSkyColor(original.ridgeBack, palette[1]),
    ridgeFront: tintWrappedSkyColor(original.ridgeFront, palette[0]),
  };
};

export const skyRidge = (
  base: number,
  amp1: number,
  freq1: number,
  phase1: number,
  amp2: number,
  freq2: number,
  phase2: number,
): string => {
  let path = `M 0 340 L 0 ${base.toFixed(1)}`;

  for (let x = 0; x <= 900; x += 6) {
    const y =
      base -
      Math.abs(Math.sin((x / 900) * Math.PI * freq1 + phase1)) * amp1 -
      Math.abs(Math.sin((x / 900) * Math.PI * freq2 + phase2)) * amp2 * 0.6;

    path += ` L ${x} ${y.toFixed(1)}`;
  }

  return path + " L 900 340 Z";
};
