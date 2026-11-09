import type { GalleryMonth, GalleryPhoto } from "@/api/gallery";

import { type JustifiedRow, aspectOf, buildJustifiedRows } from "./justified-layout";
import { monthKey, monthLabel, monthName } from "./months";

export interface TimelineMetrics {
  containerWidth: number;
  targetHeight: number;
  gap: number;
  headerHeight: number;
  yearHeaderHeight: number;
  // The first year sits right under the toolbar, so it skips the spacing that separates years.
  leadingYearHeaderHeight: number;
}

export type TimelineItem =
  | { kind: "year"; id: string; monthKey: string; year: number; count: number; size: number }
  | { kind: "header"; id: string; monthKey: string; label: string; count: number; size: number }
  | { kind: "row"; id: string; monthKey: string; row: JustifiedRow<GalleryPhoto>; size: number }
  | { kind: "placeholder"; id: string; monthKey: string; size: number };

export interface TimelineSection {
  key: string;
  year: number;
  month: number;
  label: string;
  start: number;
  end: number;
}

export interface TimelineLayout {
  items: TimelineItem[];
  sections: TimelineSection[];
  totalHeight: number;
}

export interface ScrubberMark {
  year: number;
  fraction: number;
}

// Average photo shape used to size months that have not loaded yet. Phones and cameras shoot
// 4:3 or 3:2, so the estimate lands close and the correction when a month loads stays small.
const ESTIMATED_ASPECT = 4 / 3;

const estimates = new Map<string, number>();
const MAX_ESTIMATES = 2_000;

// Runs the real packing over stand-in photos, so the estimate follows the layout rule exactly
// instead of approximating it. Memoized because every month load re-lays the whole timeline.
export const estimateMonthHeight = (count: number, metrics: TimelineMetrics) => {
  if (count <= 0) return 0;

  const { containerWidth, gap, targetHeight } = metrics;
  const key = `${count}:${containerWidth}:${targetHeight}:${gap}`;
  const cached = estimates.get(key);
  if (cached !== undefined) return cached;

  const standIns = Array.from({ length: count }, () => ESTIMATED_ASPECT);
  const rows = buildJustifiedRows(standIns, (aspect) => aspect, metrics);
  const height = rows.reduce((sum, row) => sum + row.height + gap, 0);

  // Resizing produces new widths; dropping the lot now and then keeps the cache bounded.
  if (estimates.size >= MAX_ESTIMATES) estimates.clear();
  estimates.set(key, height);

  return height;
};

const countsByYear = (months: GalleryMonth[]) => {
  const counts = new Map<number, number>();

  for (const { year, count } of months) counts.set(year, (counts.get(year) ?? 0) + count);

  return counts;
};

// A loaded month becomes its justified rows; an unloaded one a single placeholder sized from
// its count, which the rows replace once the month arrives.
const monthBody = (
  key: string,
  count: number,
  photos: GalleryPhoto[] | undefined,
  metrics: TimelineMetrics,
): TimelineItem[] => {
  if (photos === undefined) {
    return [
      {
        id: `p-${key}`,
        kind: "placeholder",
        monthKey: key,
        size: estimateMonthHeight(count, metrics),
      },
    ];
  }

  const rows = buildJustifiedRows(photos, (photo) => aspectOf(photo.width, photo.height), metrics);

  return rows.map((row, index) => ({
    id: `r-${key}-${index}`,
    kind: "row",
    monthKey: key,
    row,
    size: row.height + metrics.gap,
  }));
};

/**
 * Flattens the months into the scroller's item list: a header for each year, a header per
 * month, then either the month's justified rows or one placeholder sized from its count. A
 * year header belongs to the section of its newest month, so seeking to a year lands on it.
 * Section offsets let the scrubber and the scroll anchoring work in pixels without asking the
 * scroller.
 */
export const buildTimeline = (
  months: GalleryMonth[],
  loaded: ReadonlyMap<string, GalleryPhoto[]>,
  metrics: TimelineMetrics,
): TimelineLayout => {
  const items: TimelineItem[] = [];
  const sections: TimelineSection[] = [];
  const yearCounts = countsByYear(months);
  let offset = 0;
  let currentYear: number | null = null;

  for (const { year, month, count } of months) {
    const key = monthKey(year, month);
    const start = offset;

    if (year !== currentYear) {
      const size =
        currentYear === null ? metrics.leadingYearHeaderHeight : metrics.yearHeaderHeight;

      items.push({
        count: yearCounts.get(year) ?? 0,
        id: `y-${year}`,
        kind: "year",
        monthKey: key,
        size,
        year,
      });
      offset += size;
      currentYear = year;
    }

    items.push({
      kind: "header",
      id: `h-${key}`,
      monthKey: key,
      label: monthName(month),
      count,
      size: metrics.headerHeight,
    });
    offset += metrics.headerHeight;

    for (const item of monthBody(key, count, loaded.get(key), metrics)) {
      items.push(item);
      offset += item.size;
    }

    sections.push({ end: offset, key, label: monthLabel(year, month), month, start, year });
  }

  return { items, sections, totalHeight: offset };
};

/** The section containing a pixel offset, clamped to the first and last sections. */
export const sectionAt = (sections: TimelineSection[], offset: number) => {
  if (sections.length === 0) return undefined;

  let low = 0;
  let high = sections.length - 1;

  while (low < high) {
    const mid = Math.ceil((low + high) / 2);

    if (sections[mid].start <= offset) low = mid;
    else high = mid - 1;
  }

  return sections[low];
};

/** The pixel span of the row holding a photo, or null when that photo is not laid out. */
export const rowSpanOf = (layout: TimelineLayout, fileId: string) => {
  let offset = 0;

  for (const item of layout.items) {
    if (item.kind === "row" && item.row.tiles.some((tile) => tile.item.fileId === fileId)) {
      return { end: offset + item.size, start: offset };
    }

    offset += item.size;
  }

  return null;
};

/** Sections overlapping a pixel range, for loading what is on or near the screen. */
export const sectionsBetween = (sections: TimelineSection[], top: number, bottom: number) =>
  sections.filter((section) => section.end > top && section.start < bottom);

/**
 * One dot per month at the fraction a seek to that month uses, so the rail shows where photos
 * cluster. Dots closer than `minGapPx` to the previous kept one are dropped so they never merge.
 */
export const monthDots = (
  layout: TimelineLayout,
  scrollRange: number,
  trackHeight: number,
  minGapPx: number,
) => {
  const dots: number[] = [];

  if (scrollRange <= 0 || trackHeight <= 0) return dots;

  let lastPx = Number.NEGATIVE_INFINITY;

  for (const section of layout.sections) {
    const fraction = Math.min(1, section.start / scrollRange);
    const px = fraction * trackHeight;

    if (px - lastPx >= minGapPx) {
      dots.push(fraction);
      lastPx = px;
    }
  }

  return dots;
};

/**
 * One mark per year at the top of its newest month, dropping marks that would sit closer than
 * `minGapPx` to the previous kept one, so a long decade on a short screen stays readable.
 * Fractions are of `scrollRange`, the same range a seek maps onto, so clicking a label lands
 * on that year.
 */
export const yearMarks = (
  layout: TimelineLayout,
  scrollRange: number,
  trackHeight: number,
  minGapPx: number,
) => {
  const marks: ScrubberMark[] = [];

  if (scrollRange <= 0 || trackHeight <= 0) return marks;

  let lastYear: number | null = null;
  let lastPx = Number.NEGATIVE_INFINITY;

  for (const section of layout.sections) {
    const isNewYear = section.year !== lastYear;
    const fraction = Math.min(1, section.start / scrollRange);
    const px = fraction * trackHeight;

    if (isNewYear && px - lastPx >= minGapPx) {
      marks.push({ fraction, year: section.year });
      lastPx = px;
    }

    lastYear = section.year;
  }

  return marks;
};
