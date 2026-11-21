export interface NoteHeading {
  level: number;
  text: string;
  // Zero-based line where the heading starts in the markdown, for following the source cursor.
  line: number;
}

const ATX_HEADING = /^ {0,3}(?<hashes>#{1,6})(?:[ \t]+(?<text>.*?))?(?:[ \t]+#+)?[ \t]*$/u;
const SETEXT_UNDERLINE = /^ {0,3}(?<marks>=+|-+)[ \t]*$/u;
const FENCE = /^ {0,3}(?<fence>`{3,}|~{3,})/u;
const BLOCK_START = /^ {0,3}(?:[>#\-+*]|\d+[.)])/u;

// Outline entries show what a reader sees, so inline syntax is dropped from the label.
const toPlainText = (text: string): string =>
  text
    .replace(/!?\[(?<label>[^\]]*)\]\([^)]*\)/gu, "$<label>")
    .replace(/<(?<url>[^>\s]+)>/gu, "$<url>")
    .replace(/[*_~`]/gu, "")
    .trim();

const isParagraphLine = (line: string): boolean => {
  if (line.trim() === "") return false;
  if (/^ {4}/u.test(line)) return false;
  if (SETEXT_UNDERLINE.test(line) || FENCE.test(line)) return false;

  return !BLOCK_START.test(line);
};

/**
 * Headings at the top level of the document, in order. Headings inside quotes, lists or code
 * are left out because the editor renders those as nested blocks, which keeps the nth entry
 * here equal to the nth top-level heading on screen.
 */
export const extractHeadings = (markdown: string): NoteHeading[] => {
  const headings: NoteHeading[] = [];
  const lines = markdown.replace(/\r\n?/gu, "\n").split("\n");
  let fence: string | null = null;

  lines.forEach((line, index) => {
    const openedFence = FENCE.exec(line)?.groups?.fence;

    if (fence !== null) {
      if (openedFence?.startsWith(fence)) fence = null;
      return;
    }

    if (openedFence) {
      fence = openedFence;
      return;
    }

    const atx = ATX_HEADING.exec(line)?.groups;

    if (atx?.hashes) {
      headings.push({ level: atx.hashes.length, line: index, text: toPlainText(atx.text ?? "") });
      return;
    }

    const marks = SETEXT_UNDERLINE.exec(line)?.groups?.marks;

    if (!marks) return;

    // A setext heading is the paragraph right above a === or --- line, however many lines long.
    const paragraph: string[] = [];

    for (let cursor = index - 1; cursor >= 0 && isParagraphLine(lines[cursor] ?? ""); cursor--) {
      paragraph.unshift(lines[cursor] ?? "");
    }

    if (paragraph.length === 0) return;

    const level = marks.startsWith("=") ? 1 : 2;

    headings.push({
      level,
      line: index - paragraph.length,
      text: toPlainText(paragraph.join(" ")),
    });
  });

  return headings;
};

/** Zero-based line of a character offset, counted without copying the text. */
export const lineAtOffset = (text: string, offset: number): number => {
  let line = 0;
  let next = text.indexOf("\n");

  while (next !== -1 && next < offset) {
    line++;
    next = text.indexOf("\n", next + 1);
  }

  return line;
};

/** The heading a line belongs to: the last one starting at or above it, or null before the first. */
export const headingAtLine = (headings: NoteHeading[], line: number): number | null => {
  let active: number | null = null;

  headings.forEach((heading, index) => {
    if (heading.line <= line) active = index;
  });

  return active;
};
