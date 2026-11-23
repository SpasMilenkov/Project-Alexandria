import { describe, expect, it } from "vitest";

import { LinkTargetKind } from "@/enums/link-target-kind";
import { noteAboutRequest, snippetParts } from "@/features/notes/backlinks";

const ID = "3f2a9c1e-7b4d-4e8a-9c2f-1a2b3c4d5e6f";
const OTHER = "0b1c2d3e-4f50-4617-8899-aabbccddeeff";

describe("snippetParts", () => {
  it("keeps plain text as one part", () => {
    expect(snippetParts("Just a line")).toEqual([{ text: "Just a line", type: "text" }]);
  });

  it("turns an autolink into a link shown by its live name", () => {
    expect(snippetParts(`See <alexandria://file/${ID}> soon`)).toEqual([
      { text: "See ", type: "text" },
      { label: null, ref: { id: ID, kind: LinkTargetKind.File, suffix: "" }, type: "link" },
      { text: " soon", type: "text" },
    ]);
  });

  it("keeps the label of labelled links and embeds, without escapes", () => {
    const parts = snippetParts(
      `[Trip \\[draft\\]](alexandria://folder/${ID}) and ![cover](alexandria://file/${OTHER}?t=9)`,
    );

    expect(parts).toEqual([
      {
        label: "Trip [draft]",
        ref: { id: ID, kind: LinkTargetKind.Folder, suffix: "" },
        type: "link",
      },
      { text: " and ", type: "text" },
      { label: "cover", ref: { id: OTHER, kind: LinkTargetKind.File, suffix: "?t=9" }, type: "link" },
    ]);
  });

  it("falls back to the live name for an empty label", () => {
    const [part] = snippetParts(`[ ](alexandria://tag/${ID})`);

    expect(part).toMatchObject({ label: null, type: "link" });
  });

  it("drops emphasis and code markers from the text", () => {
    expect(snippetParts("**Bold** and `code` and ~~gone~~")).toEqual([
      { text: "Bold and code and gone", type: "text" },
    ]);
  });

  it("leaves a link with a malformed id as text", () => {
    expect(snippetParts("<alexandria://file/not-an-id>")).toEqual([
      { text: "<alexandria://file/not-an-id>", type: "text" },
    ]);
  });

  it("keeps the ellipsis of a cut line", () => {
    const parts = snippetParts(`…the end of <alexandria://file/${ID}>`);

    expect(parts[0]).toEqual({ text: "…the end of ", type: "text" });
  });
});

describe("noteAboutRequest", () => {
  it("names the note after the file without its extension and links to it", () => {
    expect(noteAboutRequest({ id: ID, kind: LinkTargetKind.File, name: "Lecture 3.mp4" })).toEqual({
      content: `<alexandria://file/${ID}>\n\n`,
      title: "Lecture 3",
    });
  });

  it("keeps a folder's whole name", () => {
    expect(noteAboutRequest({ id: ID, kind: LinkTargetKind.Folder, name: "Paris.2026" }).title).toBe(
      "Paris.2026",
    );
  });

  it("leaves the title to the server when the name is only an extension", () => {
    expect(noteAboutRequest({ id: ID, kind: LinkTargetKind.File, name: ".env" }).title).toBeNull();
  });
});
