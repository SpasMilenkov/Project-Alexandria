/** Limits `value` to [min, max]; an inverted range collapses to `min`. */
export const clamp = (value: number, min: number, max: number) =>
  Math.max(min, Math.min(max, value));
