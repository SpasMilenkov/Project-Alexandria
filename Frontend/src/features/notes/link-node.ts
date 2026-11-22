import { Node } from "@tiptap/core";

import { LinkTargetKind } from "@/enums/link-target-kind";

import {
  type NoteLinkRef,
  findNoteLinkStart,
  formatAppHref,
  matchNoteLink,
  parseAppHref,
  renderNoteLinkMarkdown,
  sanitizePastedLabel,
} from "./links";

export const NOTE_LINK_NODE = "noteLink";

export interface NoteLinkAttributes extends NoteLinkRef {
  label: string | null;
}

export const noteLinkContent = (ref: NoteLinkRef, label: string | null = null) => ({
  attrs: { ...ref, label } satisfies NoteLinkAttributes,
  type: NOTE_LINK_NODE,
});

const attributesOf = (attrs: Record<string, unknown>): NoteLinkAttributes => ({
  id: String(attrs.id ?? ""),
  kind: Number(attrs.kind) as LinkTargetKind,
  label: typeof attrs.label === "string" ? attrs.label : null,
  suffix: String(attrs.suffix ?? ""),
});

/**
 * An ID link shown as a chip. It reads and writes both text forms, `<alexandria://...>` and
 * `[label](alexandria://...)`, exactly as written, so the round-trip check on open still holds.
 * The chip component is attached by the editor, which keeps this file free of UI.
 */
export const NoteLinkNode = Node.create({
  addAttributes() {
    return {
      id: { default: "" },
      kind: { default: LinkTargetKind.File },
      label: { default: null },
      suffix: { default: "" },
    };
  },

  atom: true,
  group: "inline",
  inline: true,

  markdownTokenizer: {
    level: "inline",
    name: NOTE_LINK_NODE,
    start: findNoteLinkStart,
    tokenize: (src) => {
      const match = matchNoteLink(src);

      if (!match) return undefined;

      return { label: match.label, raw: match.raw, ref: match.ref, type: NOTE_LINK_NODE };
    },
  },

  name: NOTE_LINK_NODE,

  // Copying between notes goes through HTML, so the chip carries its link in data attributes.
  parseHTML() {
    return [
      {
        getAttrs: (element) => {
          const ref = parseAppHref(element.getAttribute("data-note-link") ?? "");

          if (!ref) return false;

          return { ...ref, label: sanitizePastedLabel(element.getAttribute("data-label")) };
        },
        tag: "span[data-note-link]",
      },
    ];
  },

  parseMarkdown: (token, helpers) =>
    helpers.createNode(NOTE_LINK_NODE, { ...token.ref, label: token.label }),

  renderHTML({ node }) {
    const attrs = attributesOf(node.attrs);
    const html: Record<string, string> = { "data-note-link": formatAppHref(attrs) };

    if (attrs.label !== null) html["data-label"] = attrs.label;

    return ["span", html, attrs.label ?? formatAppHref(attrs)];
  },

  renderMarkdown: (node) => {
    const attrs = attributesOf(node.attrs ?? {});

    return renderNoteLinkMarkdown(attrs, attrs.label);
  },

  // Plain-text copies keep the link itself, which pastes back as a chip.
  renderText({ node }) {
    const attrs = attributesOf(node.attrs);

    return renderNoteLinkMarkdown(attrs, attrs.label);
  },

  selectable: true,
});
