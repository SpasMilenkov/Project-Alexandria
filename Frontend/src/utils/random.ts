// Seeded generators shared by the generated artwork, so the same seed draws the same art in
// every surface (on screen, exported images, previews).

/** Mulberry32: a fast 32-bit PRNG returning floats in [0, 1). */
export const mulberry32 = (seed: number) => () => {
  seed |= 0;
  seed = (seed + 0x6d2b79f5) | 0;

  let t = Math.imul(seed ^ (seed >>> 15), 1 | seed);

  t ^= t + Math.imul(t ^ (t >>> 7), 61 | t);

  return ((t ^ (t >>> 14)) >>> 0) / 4294967296;
};

/** FNV-1a over UTF-16 code units, as an unsigned 32-bit integer. */
export const hashString = (value: string): number => {
  let hash = 2166136261;

  for (let index = 0; index < value.length; index++) {
    hash ^= value.charCodeAt(index);
    hash = Math.imul(hash, 16777619);
  }

  return hash >>> 0;
};
