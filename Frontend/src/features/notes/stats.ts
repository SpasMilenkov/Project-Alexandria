// Letters or digits, joined by apostrophes or hyphens, so "don't" and "well-known" are one word.
const WORD = /[\p{L}\p{N}]+(?:['’-][\p{L}\p{N}]+)*/gu;

// An average adult reading pace for prose on screen.
const WORDS_PER_MINUTE = 230;

// Link and image targets are addresses, not words a reader reads.
const stripLinkTargets = (markdown: string): string =>
  markdown.replace(/\]\([^)]*\)/gu, "]").replace(/<[a-z][a-z0-9+.-]*:[^>\s]*>/giu, "");

export const countWords = (markdown: string): number =>
  stripLinkTargets(markdown).match(WORD)?.length ?? 0;

/** Whole minutes to read, rounded, never below one for a note that has any words. */
export const readingMinutes = (wordCount: number): number => {
  if (wordCount === 0) return 0;

  return Math.max(1, Math.round(wordCount / WORDS_PER_MINUTE));
};
