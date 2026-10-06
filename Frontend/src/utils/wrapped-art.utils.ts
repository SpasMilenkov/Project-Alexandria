import type { WrappedCardFacts, WrappedCardResponse, WrappedPeriodPoint } from "@/api/stats";

import { WrappedCardType } from "@/enums/wrapped-card-type";

type WrappedArtFamily = "orbit" | "ribbons" | "constellation";

interface ArtPath {
  d: string;
  fill: string;
  stroke?: string;
  width?: number;
}

interface ArtCircle {
  x: number;
  y: number;
  r: number;
  fill: string;
  stroke?: string;
  width?: number;
}

interface WrappedRecipe {
  family: WrappedArtFamily;
  variant: number;
  inks: string[];
  paths: ArtPath[];
  circles: ArtCircle[];
  caption: string;
  metric: string;
}

export const bounded = (value: number | undefined, min = 0, max = 1): number => {
  if (value === undefined || !Number.isFinite(value)) return min;

  return Math.min(max, Math.max(min, value));
};

const wrappedHash = (value: string): number => {
  let hash = 2166136261;

  for (const char of value) hash = Math.imul(hash ^ char.charCodeAt(0), 16777619);

  return hash >>> 0;
};

export const factsFor = (card: WrappedCardResponse): WrappedCardFacts => ({
  seconds: bounded(card.facts?.seconds, 0, 1e12),
  count: bounded(card.facts?.count, 0, 1e7),
  share: bounded(card.facts?.share),
  baselineSeconds: bounded(card.facts?.baselineSeconds, 0, 1e12),
  from: card.facts?.from,
  to: card.facts?.to,
  detail: card.facts?.detail,
  comparison: card.facts?.comparison,
  comparisonAlternatives: card.facts?.comparisonAlternatives,
  rhythm: card.facts?.rhythm,
  weights: (card.facts?.weights ?? []).slice(0, 24).map((value) => bounded(value, 0, 1e12)),

  series: (card.facts?.series ?? [])
    .slice(0, 366)
    .filter((point) => Number.isFinite(Date.parse(point.date)))
    .map((point) => ({ ...point, seconds: bounded(point.seconds, 0, 1e12) })),
});

const PALETTES: Record<string, string[]> = {
  "midnight-blue": ["var(--wrapped-blue)", "var(--wrapped-purple)", "var(--wrapped-leaf)"],
  "pale-gold": ["var(--wrapped-gold)", "var(--wrapped-sage)", "var(--wrapped-clay)"],
  daylight: ["var(--wrapped-teal)", "var(--wrapped-sky)", "var(--wrapped-olive)"],
  ember: ["var(--wrapped-rose)", "var(--wrapped-lilac)", "var(--wrapped-sand)"],
};

// Only stories still drawn by the shared renderer belong here.
const FAMILIES: Partial<Record<WrappedCardType, WrappedArtFamily>> = {
  [WrappedCardType.Exploration]: "orbit",
  [WrappedCardType.Discoveries]: "constellation",
  [WrappedCardType.NewArtists]: "constellation",
  [WrappedCardType.RetainedDiscovery]: "constellation",
  [WrappedCardType.Chapters]: "ribbons",
};

export const wrappedInks = (card: WrappedCardResponse): string[] => {
  const palette = PALETTES[card.visual.colorKey] ?? PALETTES["midnight-blue"]!;
  const shift = card.type % 3;

  return [palette[shift]!, palette[(shift + 1) % 3]!, palette[(shift + 2) % 3]!];
};

const point = (x: number, y: number): string => `${x.toFixed(2)} ${y.toFixed(2)}`;

const polar = (angle: number, radius: number, cx = 400, cy = 180) => ({
  x: cx + Math.cos(angle) * radius,
  y: cy + Math.sin(angle) * radius,
});

export const buildWrappedRecipe = (
  card: WrappedCardResponse,
  seed: string,
): WrappedRecipe | null => {
  const family = FAMILIES[card.type as WrappedCardType];

  if (!family) return null;

  const facts = factsFor(card);
  const variant = wrappedHash(`${seed}:${card.id ?? card.type}:v1`) % 3;
  const inks = wrappedInks(card);
  const [a, , c] = inks as [string, string, string];

  const recipe: WrappedRecipe = {
    family,
    variant,
    inks,
    paths: [],
    circles: [],
    caption: "",
    metric: "",
  };

  const line = (x1: number, y1: number, x2: number, y2: number, color = a, width = 1) => {
    recipe.paths.push({
      d: `M ${point(x1, y1)} L ${point(x2, y2)}`,
      fill: "none",
      stroke: color,
      width,
    });
  };

  if (family === "orbit") {
    const share = facts.share;
    const radius = 120;
    const end = polar(share * Math.PI * 2 - Math.PI / 2, radius);

    recipe.circles.push({
      x: 400,
      y: 180,
      r: radius,
      fill: "none",
      stroke: "var(--wrapped-track)",
      width: 32,
    });

    if (share >= 0.9999)
      recipe.circles.push({ x: 400, y: 180, r: radius, fill: "none", stroke: a, width: 32 });
    else if (share > 0)
      recipe.paths.push({
        d: `M 400 60 A 120 120 0 ${share > 0.5 ? 1 : 0} 1 ${point(end.x, end.y)}`,
        fill: "none",
        stroke: a,
        width: 32,
      });

    recipe.circles.push({ x: 400, y: 180, r: 64 * Math.sqrt(share), fill: c });
    recipe.caption = "New listening in color · familiar listening in the outer track";
    recipe.metric = `${Math.round(share * 100)}% of your listening`;
  }

  if (recipe.family === "ribbons") {
    const series = facts.series;
    const max = Math.max(1, ...series.map((p) => p.seconds));
    const leaders = [...new Set(series.map((p) => p.leaderKey).filter(Boolean))];
    const step = 656 / Math.max(1, series.length);

    series.forEach((month, index) => {
      const x = 72 + index * step;
      const y = 180;
      const half = (month.seconds / max) * 100;
      const nextHalf = ((series[index + 1]?.seconds ?? month.seconds) / max) * 100;
      const color = inks[Math.max(0, leaders.indexOf(month.leaderKey)) % 3]!;

      recipe.paths.push({
        d: `M ${point(x, y - half)} C ${point(x + step / 2, y - half)} ${point(x + step / 2, y - nextHalf)} ${point(x + step, y - nextHalf)} L ${point(x + step, y + nextHalf)} C ${point(x + step / 2, y + nextHalf)} ${point(x + step / 2, y + half)} ${point(x, y + half)} Z`,
        fill: color,
      });

      line(x, 298, x, 312, "var(--wrapped-line)");
    });

    recipe.caption = "Ribbon width follows listening time · colors follow the monthly leader";
    recipe.metric = `${facts.count} changes in your soundtrack`;
  }

  if (family === "constellation") {
    const unit = Math.max(1, Math.ceil(facts.count / 60));
    const count = Math.min(60, Math.ceil(facts.count / unit));

    for (let i = 0; i < count; i++) {
      const angle = i * 2.399963 + variant * 0.4;
      const radius = Math.sqrt((i + 1) / Math.max(1, count)) * 138;
      const p = polar(angle, radius);

      if (i % 3 === 0) line(400, 180, p.x, p.y, "var(--wrapped-line)");

      recipe.circles.push({ ...p, r: 5 + (i % 3) * 2, fill: inks[i % 3]! });
    }

    const subject =
      card.type === WrappedCardType.RetainedDiscovery ? "listening dates" : "discoveries";

    recipe.caption =
      unit > 1
        ? `Each dot represents up to ${unit} ${subject}`
        : "One point for each discovery or listening date";

    recipe.metric = `${facts.count} moments of discovery`;
  }

  return recipe;
};

export const formatWrappedDuration = (seconds: number): string => {
  if (seconds < 60) return `${Math.round(seconds)} sec`;
  if (seconds < 3600) return `${Math.round(seconds / 60)} min`;

  return `${new Intl.NumberFormat(undefined, { maximumFractionDigits: 1 }).format(seconds / 3600)} hr`;
};

export const monthLabel = (point: WrappedPeriodPoint): string =>
  new Intl.DateTimeFormat(undefined, { month: "short", timeZone: "UTC" }).format(
    new Date(point.date),
  );
