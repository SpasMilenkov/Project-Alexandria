import type { ContentMatchSegment } from "@/api/file";

// Letters and digits, the same words the server splits a query into.
const WORD = /[\p{L}\p{N}]+/gu;

export const queryWords = (query: string) => query.toLowerCase().match(WORD) ?? [];

const splitOnMatches = (text: string, pattern: RegExp): ContentMatchSegment[] => {
  const parts: ContentMatchSegment[] = [];
  let last = 0;

  for (const match of text.matchAll(pattern)) {
    const start = match.index;
    if (start > last) {
      parts.push({ isMatch: false, text: text.slice(last, start) });
    }
    parts.push({ isMatch: true, text: match[0] });
    last = start + match[0].length;
  }

  if (last < text.length) {
    parts.push({ isMatch: false, text: text.slice(last) });
  }
  return parts;
};

// Also marks every word starting with one of the typed words. The server marks only the
// words its text search used, which leaves out words under three letters, so "ML-KEM"
// came back with just "KEM" marked.
export const markQueryWords = (
  segments: ContentMatchSegment[],
  query: string,
): ContentMatchSegment[] => {
  const words = queryWords(query);
  if (words.length === 0) {
    return segments;
  }

  const pattern = new RegExp(`(?<![\\p{L}\\p{N}])(?:${words.join("|")})[\\p{L}\\p{N}]*`, "giu");
  return segments.flatMap((segment) =>
    segment.isMatch ? [segment] : splitOnMatches(segment.text, pattern),
  );
};

// A one-line excerpt cuts off at its end, so the text before the first match is shortened
// to its last few words; otherwise the match itself could be cut off.
export const leadIntoFirstMatch = (
  segments: ContentMatchSegment[],
  maxLead: number,
): ContentMatchSegment[] => {
  const first = segments.findIndex((segment) => segment.isMatch);
  if (first <= 0) {
    return segments;
  }

  const before = segments
    .slice(0, first)
    .map((segment) => segment.text)
    .join("");
  if (before.length <= maxLead) {
    return segments;
  }

  const tail = before.slice(-maxLead);
  const wordStart = tail.indexOf(" ");
  const lead = wordStart === -1 ? tail : tail.slice(wordStart + 1);
  if (lead.length === 0) {
    return segments.slice(first);
  }
  return [{ isMatch: false, text: lead }, ...segments.slice(first)];
};
