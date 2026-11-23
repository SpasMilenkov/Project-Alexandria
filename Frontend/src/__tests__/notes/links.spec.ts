import { describe, expect, it } from "vitest";

import { LinkTargetKind } from "@/enums/link-target-kind";
import {
  extractNoteLinks,
  findNoteEmbedStart,
  findNoteLinkStart,
  formatAppHref,
  labelText,
  linkKey,
  matchNoteEmbed,
  matchNoteLink,
  parseAppHref,
  pastedLinkRef,
  renderNoteEmbedMarkdown,
  renderNoteLinkMarkdown,
  sanitizePastedLabel,
} from "@/features/notes/links";

const ID = "3f2a9c1e-7b4d-4e8a-9c2f-1a2b3c4d5e6f";
const OTHER = "0b1c2d3e-4f50-4617-8899-aabbccddeeff";
const ORIGIN = "https://alexandria.local";

describe("parseAppHref", () => {
  it("reads each kind", () => {
    expect(parseAppHref(`alexandria://file/${ID}`)?.kind).toBe(LinkTargetKind.File);
    expect(parseAppHref(`alexandria://folder/${ID}`)?.kind).toBe(LinkTargetKind.Folder);
    expect(parseAppHref(`alexandria://tag/${ID}`)?.kind).toBe(LinkTargetKind.Tag);
    expect(parseAppHref(`alexandria://playlist/${ID}`)?.kind).toBe(LinkTargetKind.Playlist);
  });

  it("keeps the query and fragment as written", () => {
    expect(parseAppHref(`alexandria://file/${ID}?t=90#page=3`)).toEqual({
      id: ID,
      kind: LinkTargetKind.File,
      suffix: "?t=90#page=3",
    });
  });

  it("refuses unknown kinds and ids that are not GUIDs", () => {
    expect(parseAppHref(`alexandria://user/${ID}`)).toBeNull();
    expect(parseAppHref("alexandria://file/not-a-guid")).toBeNull();
    expect(parseAppHref(`https://example.com/file/${ID}`)).toBeNull();
  });

  it("formats back to the same text", () => {
    const href = `alexandria://playlist/${ID.toUpperCase()}#x`;

    expect(formatAppHref(parseAppHref(href)!)).toBe(href);
  });
});

describe("matchNoteLink", () => {
  it("reads an autolink without a label", () => {
    const match = matchNoteLink(`<alexandria://file/${ID}> and more`);

    expect(match?.raw).toBe(`<alexandria://file/${ID}>`);
    expect(match?.label).toBeNull();
  });

  it("reads a labelled link and keeps the label's source", () => {
    const match = matchNoteLink(`[Paris \\[2024\\]](alexandria://folder/${ID}) rest`);

    expect(match?.raw).toBe(`[Paris \\[2024\\]](alexandria://folder/${ID})`);
    expect(match?.label).toBe("Paris \\[2024\\]");
    expect(labelText(match!.label!)).toBe("Paris [2024]");
  });

  it("ignores ordinary links and text", () => {
    expect(matchNoteLink("[site](https://example.com)")).toBeNull();
    expect(matchNoteLink("<https://example.com>")).toBeNull();
    expect(matchNoteLink("plain [brackets] here")).toBeNull();
  });

  it("only matches at the start", () => {
    expect(matchNoteLink(`see <alexandria://file/${ID}>`)).toBeNull();
  });
});

describe("findNoteLinkStart", () => {
  it("finds the first link after other brackets", () => {
    const src = `a [b] c [d](alexandria://tag/${ID})`;

    expect(findNoteLinkStart(src)).toBe(src.indexOf("[d]"));
  });

  it("skips images and escaped brackets", () => {
    expect(findNoteLinkStart(`![cover](alexandria://file/${ID})`)).toBe(-1);
    expect(findNoteLinkStart(`\\[x](alexandria://file/${ID})`)).toBe(-1);
  });

  it("finds autolinks", () => {
    expect(findNoteLinkStart(`text <alexandria://file/${ID}>`)).toBe(5);
  });
});

describe("renderNoteLinkMarkdown", () => {
  it.each([
    `<alexandria://file/${ID}>`,
    `[My label](alexandria://file/${ID}?t=12)`,
    `[](alexandria://tag/${ID})`,
    `[a \\* b](alexandria://folder/${ID})`,
  ])("writes %s back exactly as it was read", (source) => {
    const match = matchNoteLink(source)!;

    expect(renderNoteLinkMarkdown(match.ref, match.label)).toBe(source);
  });

  it("escapes a label that would otherwise close the link and open another", () => {
    const ref = { id: ID, kind: LinkTargetKind.File, suffix: "" };
    const label = "notes](https://example.com) [x";

    const markdown = renderNoteLinkMarkdown(ref, label);
    const match = matchNoteLink(markdown)!;

    expect(match.raw).toBe(markdown);
    expect(match.ref).toEqual(ref);
    expect(labelText(match.label!)).toBe(label);
  });
});

describe("sanitizePastedLabel", () => {
  const ref = { id: ID, kind: LinkTargetKind.File, suffix: "" };

  // Each one is written as a single link to the chip's own target, showing the text unchanged.
  it.each([
    "notes](https://example.com) [x",
    "[evil](https://example.com)",
    "a | b",
    "`code` and *stars* and _under_ and ~strike~",
    "<https://example.com>",
    String.raw`trailing backslash \\`,
  ])("keeps %s inside its own link", (pasted) => {
    const label = sanitizePastedLabel(pasted)!;
    const markdown = renderNoteLinkMarkdown(ref, label);
    const match = matchNoteLink(markdown)!;

    expect(match.raw).toBe(markdown);
    expect(match.ref).toEqual(ref);
    expect(labelText(match.label!)).toBe(labelText(pasted));
  });

  it("turns line breaks into spaces, since a label can't span lines", () => {
    expect(labelText(sanitizePastedLabel("first\r\n  second\nthird")!)).toBe("first second third");
  });

  it("changes nothing on a second pass, so chips copied between notes stay stable", () => {
    const once = sanitizePastedLabel("notes](https://example.com) [x")!;

    expect(sanitizePastedLabel(once)).toBe(once);
  });

  it("leaves a missing label missing, so the chip shows the target's live name", () => {
    expect(sanitizePastedLabel(null)).toBeNull();
  });
});

describe("extractNoteLinks", () => {
  it("lists distinct targets in order, ignoring id case", () => {
    const markdown = [
      `See [trip](alexandria://folder/${OTHER}) and <alexandria://file/${ID}>.`,
      `Again <alexandria://file/${ID.toUpperCase()}?t=3>`,
      `![cover](alexandria://file/${OTHER})`,
    ].join("\n");

    expect(extractNoteLinks(markdown).map(linkKey)).toEqual([
      `${LinkTargetKind.Folder}:${OTHER}`,
      `${LinkTargetKind.File}:${ID}`,
      `${LinkTargetKind.File}:${OTHER}`,
    ]);
  });

  it("skips links inside code spans and fenced blocks", () => {
    const markdown = [
      `Inline \`<alexandria://file/${ID}>\` example.`,
      "```md",
      `<alexandria://tag/${ID}>`,
      "```",
      "~~~~",
      `<alexandria://playlist/${ID}>`,
      "~~~",
      "still fenced",
      "~~~~",
      `Real <alexandria://folder/${OTHER}>`,
    ].join("\n");

    expect(extractNoteLinks(markdown)).toEqual([
      { id: OTHER, kind: LinkTargetKind.Folder, suffix: "" },
    ]);
  });

  it("returns nothing for a note without links", () => {
    expect(extractNoteLinks("# Title\n\nJust [a link](https://example.com).")).toEqual([]);
  });
});

describe("pastedLinkRef", () => {
  it("accepts an alexandria link, with or without autolink brackets", () => {
    expect(pastedLinkRef(` alexandria://tag/${ID} `, ORIGIN)?.kind).toBe(LinkTargetKind.Tag);
    expect(pastedLinkRef(`<alexandria://tag/${ID}>`, ORIGIN)?.kind).toBe(LinkTargetKind.Tag);
  });

  it("maps this app's item pages to their targets", () => {
    expect(pastedLinkRef(`${ORIGIN}/notes/${ID}`, ORIGIN)).toEqual({
      id: ID,
      kind: LinkTargetKind.File,
      suffix: "",
    });
    expect(pastedLinkRef(`${ORIGIN}/dashboard/${ID}`, ORIGIN)?.kind).toBe(LinkTargetKind.Folder);
    expect(pastedLinkRef(`${ORIGIN}/streaming/playlists/${ID}`, ORIGIN)?.kind).toBe(
      LinkTargetKind.Playlist,
    );
    expect(pastedLinkRef(`${ORIGIN}/streaming/track/${ID}`, ORIGIN)?.kind).toBe(
      LinkTargetKind.File,
    );
  });

  it("leaves other sites, other pages and longer text alone", () => {
    expect(pastedLinkRef(`https://elsewhere.example/notes/${ID}`, ORIGIN)).toBeNull();
    expect(pastedLinkRef(`${ORIGIN}/dashboard/tags`, ORIGIN)).toBeNull();
    expect(pastedLinkRef(`look at ${ORIGIN}/notes/${ID}`, ORIGIN)).toBeNull();
    expect(pastedLinkRef("", ORIGIN)).toBeNull();
  });
});

describe("note embeds", () => {
  it("reads an embed with its caption and deep-link suffix", () => {
    expect(matchNoteEmbed(`![Slides \\[v2\\]](alexandria://file/${ID}#page=3) after`)).toEqual({
      caption: String.raw`Slides \[v2\]`,
      raw: `![Slides \\[v2\\]](alexandria://file/${ID}#page=3)`,
      ref: { id: ID, kind: LinkTargetKind.File, suffix: "#page=3" },
    });
  });

  it("reads an empty caption", () => {
    expect(matchNoteEmbed(`![](alexandria://file/${ID})`)?.caption).toBe("");
  });

  it("leaves web images and malformed ids alone", () => {
    expect(matchNoteEmbed("![x](https://example.com/a.png)")).toBeNull();
    expect(matchNoteEmbed("![x](alexandria://file/nope)")).toBeNull();
  });

  it("finds the next embed, skipping an escaped one", () => {
    const text = `\\![no](alexandria://file/${ID}) then ![yes](alexandria://file/${ID})`;

    expect(findNoteEmbedStart(text)).toBe(text.lastIndexOf("![yes]"));
  });

  it("writes an embed back as read, and escapes a caption that would break out", () => {
    const ref = { id: ID, kind: LinkTargetKind.File, suffix: "?t=90" };

    expect(renderNoteEmbedMarkdown(ref, "Clip")).toBe(`![Clip](alexandria://file/${ID}?t=90)`);
    expect(renderNoteEmbedMarkdown(ref, "a](https://x.y) [b")).toBe(
      String.raw`![a\](https://x.y) \[b](alexandria://file/${ID}?t=90)`,
    );
  });
});
