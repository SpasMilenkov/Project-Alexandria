import { LinkTargetKind } from "@/enums/link-target-kind";

/** One `alexandria://<kind>/<id>[?..][#..]` link, as written in a note. */
export interface NoteLinkRef {
  kind: LinkTargetKind;
  // Kept exactly as written, so saving never changes a link's spelling.
  id: string;
  // The query and fragment as written ("?t=90", "#page=3"), or "".
  suffix: string;
}

/** A link found in markdown: the target plus the author's label, if the link has one. */
export interface NoteLinkMatch {
  raw: string;
  ref: NoteLinkRef;
  // The label's markdown source, kept as written; null for `<alexandria://...>`, which shows the
  // target's live name.
  label: string | null;
}

const KIND_SEGMENTS: Record<LinkTargetKind, string> = {
  [LinkTargetKind.File]: "file",
  [LinkTargetKind.Folder]: "folder",
  [LinkTargetKind.Tag]: "tag",
  [LinkTargetKind.Playlist]: "playlist",
};

const KINDS_BY_SEGMENT: Record<string, LinkTargetKind> = {
  file: LinkTargetKind.File,
  folder: LinkTargetKind.Folder,
  playlist: LinkTargetKind.Playlist,
  tag: LinkTargetKind.Tag,
};

const GUID = "[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}";

const HREF = new RegExp(
  `^alexandria://(?<segment>file|folder|tag|playlist)/(?<id>${GUID})(?<suffix>(?:[?#][^\\s<>()]*)?)$`,
  "u",
);

// A label may hold escaped characters but no unescaped brackets or line breaks. Shared by the
// reader and the writer, so a label is only ever written in a form that reads back as itself.
const LABEL_BODY = String.raw`(?:\\[\s\S]|[^\\[\]\n])*`;
const LABEL_SOURCE = new RegExp(`^${LABEL_BODY}$`, "u");

const AUTOLINK = /^<(?<href>alexandria:\/\/[^\s<>]+)>/u;
const LABELED = new RegExp(
  String.raw`^\[(?<label>${LABEL_BODY})\]\((?<href>alexandria:\/\/[^\s()<>]+)\)`,
  "u",
);
const LINK_START = /<alexandria:\/\/|\[/gu;

// Every place a link can appear, embeds included; used once code has been blanked out.
const ANY_LINK = new RegExp(
  `alexandria://(?<segment>file|folder|tag|playlist)/(?<id>${GUID})`,
  "gu",
);

const ESCAPABLE = /\\(?<char>[!-/:-@[-`{-~])/gu;

export const parseAppHref = (href: string): NoteLinkRef | null => {
  const groups = HREF.exec(href)?.groups;

  if (!groups) return null;

  return { id: groups.id, kind: KINDS_BY_SEGMENT[groups.segment], suffix: groups.suffix ?? "" };
};

export const formatAppHref = ({ kind, id, suffix }: NoteLinkRef): string =>
  `alexandria://${KIND_SEGMENTS[kind]}/${id}${suffix}`;

/** Ids are GUIDs, which compare without case. */
export const linkKey = ({ kind, id }: { kind: LinkTargetKind; id: string }): string =>
  `${kind}:${id.toLowerCase()}`;

/** The label as text: markdown backslash escapes removed. */
export const labelText = (label: string): string => label.replace(ESCAPABLE, "$<char>");

/** Reads an ID link at the very start of `src`, in either of its text forms. */
export const matchNoteLink = (src: string): NoteLinkMatch | null => {
  const autolink = AUTOLINK.exec(src);

  if (autolink?.groups) {
    const ref = parseAppHref(autolink.groups.href);

    if (!ref) return null;

    return { label: null, raw: autolink[0], ref };
  }

  const labeled = LABELED.exec(src);

  if (!labeled?.groups) return null;

  const ref = parseAppHref(labeled.groups.href);

  if (!ref) return null;

  return { label: labeled.groups.label, raw: labeled[0], ref };
};

/**
 * Where the next ID link starts in `src`, or -1. Brackets after "!" open an image and escaped
 * brackets are text, so neither counts.
 */
export const findNoteLinkStart = (src: string): number => {
  for (const match of src.matchAll(LINK_START)) {
    const before = src[match.index - 1];
    const opensLink = before !== "!" && before !== "\\";

    if (opensLink && matchNoteLink(src.slice(match.index))) return match.index;
  }

  return -1;
};

// Brackets and backslashes could end the label early, a pipe would split a table cell around it,
// and backticks, angle brackets and emphasis markers could pair with text outside the link.
// Escaped, each one is plain text, and `labelText` shows it exactly as before.
const LABEL_SPECIAL = /[\\[\]|`<>*_~]/gu;
const LINE_BREAKS = /\s*[\r\n]+\s*/gu;

const escapeLabel = (text: string): string =>
  text.replace(LINE_BREAKS, " ").replace(LABEL_SPECIAL, String.raw`\$&`);

/**
 * A label from pasted HTML, made safe to write as markdown. The attribute is whatever the page
 * that was copied put there, so unescaped, `notes](https://example.com) [x` would save as a web
 * link to that site that the chip never showed.
 */
export const sanitizePastedLabel = (label: string | null): string | null => {
  if (label === null) return null;

  return escapeLabel(labelText(label));
};

/** Writes a link back in the form it was read in. */
export const renderNoteLinkMarkdown = (ref: NoteLinkRef, label: string | null): string => {
  const href = formatAppHref(ref);

  if (label === null) return `<${href}>`;

  // Labels read from markdown always fit; anything else is escaped rather than allowed to break
  // out of the link, whichever path it came in by.
  if (!LABEL_SOURCE.test(label)) return `[${escapeLabel(labelText(label))}](${href})`;

  return `[${label}](${href})`;
};

const FENCE_OPEN = /^ {0,3}(?<fence>`{3,}|~{3,})/u;

// Fenced code blocks become blank lines, so line structure (and code spans around them) holds.
const blankFencedCode = (markdown: string): string => {
  const lines = markdown.split(/\r\n?|\n/u);
  let fence: string | null = null;

  return lines
    .map((line) => {
      if (fence !== null) {
        const closes = new RegExp(`^ {0,3}${fence[0]}{${fence.length},}\\s*$`, "u").test(line);

        if (closes) fence = null;

        return "";
      }

      const opening = FENCE_OPEN.exec(line)?.groups?.fence;

      if (opening) {
        fence = opening;
        return "";
      }

      return line;
    })
    .join("\n");
};

// A code span closes on a run of backticks as long as the one that opened it.
const blankCodeSpans = (text: string): string =>
  text.replace(/(?<!`)(?<ticks>`+)(?!`)[\s\S]*?(?<!`)\k<ticks>(?!`)/gu, "");

/**
 * Every distinct link target in a note, in order of first appearance. Links inside code are
 * examples, not links, so they are skipped like the server's index skips them.
 */
export const extractNoteLinks = (markdown: string): NoteLinkRef[] => {
  const text = blankCodeSpans(blankFencedCode(markdown));
  const seen = new Set<string>();
  const refs: NoteLinkRef[] = [];

  for (const { groups } of text.matchAll(ANY_LINK)) {
    const ref: NoteLinkRef = {
      id: groups?.id ?? "",
      kind: KINDS_BY_SEGMENT[groups?.segment ?? ""],
      suffix: "",
    };
    const key = linkKey(ref);

    if (!seen.has(key)) {
      seen.add(key);
      refs.push(ref);
    }
  }

  return refs;
};

// App pages that stand for one item, so pasting their address links to that item.
const APP_PATHS: { pattern: RegExp; kind: LinkTargetKind }[] = [
  { kind: LinkTargetKind.File, pattern: new RegExp(`^/notes/(?<id>${GUID})/?$`, "u") },
  { kind: LinkTargetKind.File, pattern: new RegExp(`^/streaming/track/(?<id>${GUID})/?$`, "u") },
  { kind: LinkTargetKind.Folder, pattern: new RegExp(`^/dashboard/(?<id>${GUID})/?$`, "u") },
  {
    kind: LinkTargetKind.Playlist,
    pattern: new RegExp(`^/streaming/playlists/(?<id>${GUID})/?$`, "u"),
  },
];

/**
 * The link a pasted text stands for: an `alexandria://` link, or the address of one of this
 * app's own pages for a single item. Anything else, other sites included, stays plain text.
 */
export const pastedLinkRef = (text: string, origin: string): NoteLinkRef | null => {
  // A link copied as markdown arrives in its autolink brackets.
  const trimmed = text.trim().replace(/^<(?<inner>[^<>]*)>$/u, "$<inner>");

  if (trimmed === "" || /\s/u.test(trimmed)) return null;

  const direct = parseAppHref(trimmed);

  if (direct) return direct;
  if (!URL.canParse(trimmed)) return null;

  const url = new URL(trimmed);

  if (url.origin !== origin) return null;

  for (const { pattern, kind } of APP_PATHS) {
    const id = pattern.exec(url.pathname)?.groups?.id;

    if (id) return { id, kind, suffix: "" };
  }

  return null;
};
