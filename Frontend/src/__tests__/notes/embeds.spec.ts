import { describe, expect, it } from "vitest";

import type { LinkTargetDto } from "@/api/links";

import { LinkTargetKind } from "@/enums/link-target-kind";
import { LinkTargetState } from "@/enums/link-target-state";
import { embedView } from "@/features/notes/embeds";

const file = (overrides: Partial<LinkTargetDto>): LinkTargetDto => ({
  color: null,
  directoryId: null,
  id: "3f2a9c1e-7b4d-4e8a-9c2f-1a2b3c4d5e6f",
  isEncrypted: false,
  kind: LinkTargetKind.File,
  mimeType: "image/jpeg",
  name: "Eiffel.jpg",
  state: LinkTargetState.Available,
  versionId: "0b1c2d3e-4f50-4617-8899-aabbccddeeff",
  ...overrides,
});

describe("embedView", () => {
  it("waits while the target loads", () => {
    expect(embedView(undefined, true)).toBe("pending");
  });

  it("treats a failed lookup as unavailable", () => {
    expect(embedView(undefined, false)).toBe("unavailable");
  });

  it.each([
    ["image/png", "image"],
    ["application/pdf", "pdf"],
    ["application/vnd.openxmlformats-officedocument.wordprocessingml.document", "pdf"],
    ["audio/flac", "media"],
    ["video/mp4", "media"],
    ["text/markdown", "card"],
    ["application/zip", "card"],
  ])("shows %s as %s", (mimeType, view) => {
    expect(embedView(file({ mimeType }), false)).toBe(view);
  });

  it("never previews an encrypted file", () => {
    expect(embedView(file({ isEncrypted: true }), false)).toBe("card");
  });

  it("shows folders, tags and playlists as cards", () => {
    expect(embedView(file({ kind: LinkTargetKind.Folder, mimeType: null }), false)).toBe("card");
  });

  it("keeps trashed and unavailable apart", () => {
    expect(embedView(file({ state: LinkTargetState.Trashed }), false)).toBe("trashed");
    expect(embedView(file({ state: LinkTargetState.Unavailable }), false)).toBe("unavailable");
  });
});
