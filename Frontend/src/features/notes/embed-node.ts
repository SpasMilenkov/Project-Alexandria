import { Node } from "@tiptap/core";

import { LinkTargetKind } from "@/enums/link-target-kind";

import {
  type NoteLinkRef,
  findNoteEmbedStart,
  formatAppHref,
  matchNoteEmbed,
  parseAppHref,
  renderNoteEmbedMarkdown,
  sanitizePastedLabel,
} from "./links";

export const NOTE_EMBED_NODE = "noteEmbed";

export interface NoteEmbedAttributes extends NoteLinkRef {
  // The caption's markdown source, kept as written; "" when there is none.
  caption: string;
}

export const noteEmbedContent = (ref: NoteLinkRef, caption = "") => ({
  attrs: { ...ref, caption } satisfies NoteEmbedAttributes,
  type: NOTE_EMBED_NODE,
});

const attributesOf = (attrs: Record<string, unknown>): NoteEmbedAttributes => ({
  caption: typeof attrs.caption === "string" ? attrs.caption : "",
  id: String(attrs.id ?? ""),
  kind: Number(attrs.kind) as LinkTargetKind,
  suffix: String(attrs.suffix ?? ""),
});

/**
 * An embed, `![caption](alexandria://...)`, shown as the item itself: an image, a PDF, a player
 * card. Its tokenizer runs before the image one, so the editor never tries to load an
 * `alexandria://` address as a picture. It reads and writes the text exactly as written, like the
 * link node, so the round-trip check on open still holds. The card is attached by the editor.
 */
export const NoteEmbedNode = Node.create({
  addAttributes() {
    return {
      caption: { default: "" },
      id: { default: "" },
      kind: { default: LinkTargetKind.File },
      suffix: { default: "" },
    };
  },

  atom: true,
  draggable: true,
  group: "inline",
  inline: true,

  markdownTokenizer: {
    level: "inline",
    name: NOTE_EMBED_NODE,
    start: findNoteEmbedStart,
    tokenize: (src) => {
      const match = matchNoteEmbed(src);

      if (!match) return undefined;

      return { caption: match.caption, raw: match.raw, ref: match.ref, type: NOTE_EMBED_NODE };
    },
  },

  name: NOTE_EMBED_NODE,

  // Copying between notes goes through HTML, so the card carries its link in data attributes.
  parseHTML() {
    return [
      {
        getAttrs: (element) => {
          const ref = parseAppHref(element.getAttribute("data-note-embed") ?? "");

          if (!ref) return false;

          return { ...ref, caption: sanitizePastedLabel(element.getAttribute("data-caption")) ?? "" };
        },
        tag: "span[data-note-embed]",
      },
    ];
  },

  parseMarkdown: (token, helpers) =>
    helpers.createNode(NOTE_EMBED_NODE, { ...token.ref, caption: token.caption }),

  renderHTML({ node }) {
    const attrs = attributesOf(node.attrs);

    return [
      "span",
      { "data-caption": attrs.caption, "data-note-embed": formatAppHref(attrs) },
      attrs.caption === "" ? formatAppHref(attrs) : attrs.caption,
    ];
  },

  renderMarkdown: (node) => {
    const attrs = attributesOf(node.attrs ?? {});

    return renderNoteEmbedMarkdown(attrs, attrs.caption);
  },

  // Plain-text copies keep the embed's markdown, which pastes back as text to save.
  renderText({ node }) {
    const attrs = attributesOf(node.attrs);

    return renderNoteEmbedMarkdown(attrs, attrs.caption);
  },

  selectable: true,
});
