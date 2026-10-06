import type { WrappedCardEntryResponse, WrappedPeriodPoint } from "@/api/stats";

import { bounded } from "@/utils/wrapped-art.utils";

export const shareLabel = (share?: number): string =>
  `${new Intl.NumberFormat(undefined, { maximumFractionDigits: 1 }).format(bounded(share) * 100)}%`;

export const rankedRecords = (entries: WrappedCardEntryResponse[]) => {
  const ranked = [...entries].sort((a, b) => a.rank - b.rank).slice(0, 5);
  const maximum = Math.max(0, ...ranked.map((entry) => bounded(entry.share)));

  return ranked.map((entry) => {
    const ratio = maximum > 0 ? bounded(entry.share) / maximum : 0;

    return { entry, ratio, radius: 42 * Math.sqrt(ratio) };
  });
};

export const monthlyLandscape = (series: WrappedPeriodPoint[]) => {
  const ordered = [...series]
    .filter((month) => Number.isFinite(Date.parse(month.date)))
    .sort((a, b) => a.date.localeCompare(b.date));

  const maximum = Math.max(1, ...ordered.map((month) => month.seconds));

  return ordered.map((month) => {
    const height = bounded(month.seconds / maximum) * 150;
    // Every mound has the same footprint; its height alone encodes listening time.
    const path = `M 0 166 C 22 166 26 ${166 - height} 50 ${166 - height} C 74 ${166 - height} 78 166 100 166 Z`;

    return { month, height, path };
  });
};
