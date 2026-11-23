import type { CreateNoteRequest } from "@/api/notes";

import { LinkTargetKind } from "@/enums/link-target-kind";

import { type NoteLinkRef, formatAppHref, labelText, parseAppHref } from "./links";

/** A piece of a "Mentioned in" snippet: plain text, or a link shown by its label or live name. */
export type SnippetPart =
  | { type: "text"; text: string }
  | { type: "link"; ref: NoteLinkRef; label: string | null };

// Every form a link takes in a note: an embed or labelled link, or an autolink. The label body
// matches the one the editor reads and writes.
const LINK_TOKEN =
  /!?\[(?<label>(?:\\[\s\S]|[^\\[\]\n])*)\]\((?<href>alexandria:\/\/[^\s()<>]+)\)|<(?<auto>alexandria:\/\/[^\s<>]+)>/gu;

// Emphasis and code markers carry no meaning in a one-line preview.
const INLINE_MARKERS = /\*\*|__|~~|`/gu;

const cleanText = (text: string): string => labelText(text.replace(INLINE_MARKERS, ""));

const pushText = (parts: SnippetPart[], text: string) => {
  const cleaned = cleanText(text);

  if (cleaned !== "") parts.push({ text: cleaned, type: "text" });
};

/**
 * Splits a snippet's markdown into text and links. A link the app can't read (a malformed id)
 * stays as its text, the same way the editor leaves it alone.
 */
export const snippetParts = (snippet: string): SnippetPart[] => {
  const parts: SnippetPart[] = [];
  let position = 0;

  // Unreadable links are left in place, so they end up in the text around the next one.
  const readable = [...snippet.matchAll(LINK_TOKEN)].filter((match) =>
    parseAppHref(match.groups?.auto ?? match.groups?.href ?? ""),
  );

  for (const match of readable) {
    const groups = match.groups ?? {};
    const ref = parseAppHref(groups.auto ?? groups.href ?? "");
    const label = groups.auto === undefined ? labelText(groups.label ?? "").trim() : "";

    pushText(parts, snippet.slice(position, match.index));

    if (ref) parts.push({ label: label === "" ? null : label, ref, type: "link" });

    position = match.index + match[0].length;
  }

  pushText(parts, snippet.slice(position));

  return parts;
};

// Most files carry one extension; notes keep only their title.
const EXTENSION = /\.[^./\\\s]{1,10}$/u;

/** "New note about this": named after the file and starting with a link to it. */
export const noteAboutRequest = (target: {
  id: string;
  kind: LinkTargetKind;
  name: string;
}): CreateNoteRequest => {
  const title = target.kind === LinkTargetKind.File ? target.name.replace(EXTENSION, "") : target.name;

  return {
    content: `<${formatAppHref({ id: target.id, kind: target.kind, suffix: "" })}>\n\n`,
    title: title.trim() === "" ? null : title,
  };
};
