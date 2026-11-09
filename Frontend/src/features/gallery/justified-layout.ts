export interface JustifiedTile<T> {
  item: T;
  width: number;
}

export interface JustifiedRow<T> {
  tiles: JustifiedTile<T>[];
  height: number;
}

export interface JustifiedOptions {
  containerWidth: number;
  targetHeight: number;
  gap: number;
}

// Panoramas and tall screenshots would otherwise squash a whole row into a strip. Tiles crop
// with object-fit: cover, so clamping only trims the extremes of those few images.
const MIN_ASPECT = 0.5;
const MAX_ASPECT = 3;

export const aspectOf = (width: number | null, height: number | null) => {
  if (!width || !height) return 1;

  return Math.min(MAX_ASPECT, Math.max(MIN_ASPECT, width / height));
};

const toRow = <T>(items: T[], aspects: number[], height: number): JustifiedRow<T> => ({
  height,
  tiles: items.map((item, index) => ({ item, width: aspects[index] * height })),
});

/**
 * Packs items into rows that fill the width exactly, the way photo timelines do. Each row grows
 * until it would be at least as wide as the container at the target height, then scales down to
 * fit. The last row keeps the target height and stays left-aligned instead of stretching a few
 * photos across the full width.
 */
export const buildJustifiedRows = <T>(
  items: T[],
  aspectFor: (item: T) => number,
  { containerWidth, targetHeight, gap }: JustifiedOptions,
): JustifiedRow<T>[] => {
  const rows: JustifiedRow<T>[] = [];
  const width = Math.max(1, containerWidth);

  let pending: T[] = [];
  let aspects: number[] = [];
  let aspectSum = 0;

  for (const item of items) {
    const aspect = aspectFor(item);

    pending.push(item);
    aspects.push(aspect);
    aspectSum += aspect;

    const gaps = gap * (pending.length - 1);

    if (aspectSum * targetHeight + gaps >= width) {
      rows.push(toRow(pending, aspects, (width - gaps) / aspectSum));
      pending = [];
      aspects = [];
      aspectSum = 0;
    }
  }

  if (pending.length > 0) rows.push(toRow(pending, aspects, targetHeight));

  return rows;
};
