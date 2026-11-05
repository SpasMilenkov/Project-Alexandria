export interface RingSegmentInput {
  key: string;
  value: number;
  color: string;
}

export interface RingArc {
  key: string;
  color: string;
  // Both values are fractions of the full circumference, measured clockwise from the top.
  offset: number;
  length: number;
}

export interface RingOptions {
  gap?: number;
  minLength?: number;
}

const DEFAULT_GAP = 0.012;
const DEFAULT_MIN_LENGTH = 0.025;

// Tiny segments (previews are usually a sliver) are raised to a visible minimum; the length
// they borrow comes out of the largest segment so the ring never claims more space than is used.
const enforceMinimum = (lengths: number[], minLength: number) => {
  const raised = lengths.map((length) => Math.max(length, minLength));
  const overflow =
    raised.reduce((sum, length) => sum + length, 0) -
    lengths.reduce((sum, length) => sum + length, 0);

  if (overflow <= 0) return raised;

  const largest = raised.indexOf(Math.max(...raised));
  raised[largest] = Math.max(minLength, raised[largest] - overflow);

  return raised;
};

export const buildRingArcs = (
  segments: RingSegmentInput[],
  total: number,
  { gap = DEFAULT_GAP, minLength = DEFAULT_MIN_LENGTH }: RingOptions = {},
): RingArc[] => {
  if (total <= 0) return [];

  const visible = segments.filter((segment) => segment.value > 0);
  if (visible.length === 0) return [];

  const usedValue = visible.reduce((sum, segment) => sum + segment.value, 0);
  const usedFraction = Math.min(1, usedValue / total);

  // A full ring needs a gap after every segment to close the circle; a partial one only
  // between segments, because the free space already separates the last from the first.
  const gapCount = usedFraction >= 1 ? visible.length : visible.length - 1;
  const fullSingle = visible.length === 1 && usedFraction >= 1;
  const appliedGap = fullSingle ? 0 : gap;
  const available = Math.max(0, usedFraction - gapCount * appliedGap);

  const rawLengths = visible.map((segment) => (segment.value / usedValue) * available);
  const lengths = enforceMinimum(rawLengths, minLength);

  let offset = 0;

  return visible.map((segment, index) => {
    const arc = { color: segment.color, key: segment.key, length: lengths[index], offset };
    offset += lengths[index] + appliedGap;

    return arc;
  });
};
