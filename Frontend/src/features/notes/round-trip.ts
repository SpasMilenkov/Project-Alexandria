const HARD_BREAK = "  ";

// Two or more trailing spaces are a hard line break in markdown, so they are kept as one marker
// instead of being stripped like other trailing whitespace.
const normalizeLineEnd = (line: string): string => {
  const trailing = /[ \t]+$/u.exec(line);

  if (!trailing) return line;

  const body = line.slice(0, trailing.index);
  const isHardBreak = trailing[0].length >= 2 && !trailing[0].includes("\t");

  if (isHardBreak) return body + HARD_BREAK;

  return body;
};

/**
 * Removes differences that never change how markdown renders: line ending style, trailing
 * whitespace and blank lines around the whole document. Anything else, including extra blank
 * lines inside the text, still counts, so the check errs towards source mode.
 */
export const normalizeMarkdown = (text: string): string =>
  text
    .replace(/\r\n?/gu, "\n")
    .split("\n")
    .map(normalizeLineEnd)
    .join("\n")
    .replace(/^\n+/u, "")
    .replace(/\n+$/u, "");

/** True when the rich editor would write the document back without changing it. */
export const survivesRoundTrip = (original: string, serialized: string): boolean =>
  normalizeMarkdown(original) === normalizeMarkdown(serialized);
