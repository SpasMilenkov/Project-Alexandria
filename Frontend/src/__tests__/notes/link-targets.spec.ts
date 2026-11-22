import { describe, expect, it } from "vitest";

import type { LinkTargetDto } from "@/api/links";

import { LinkTargetKind } from "@/enums/link-target-kind";
import { LinkTargetState } from "@/enums/link-target-state";
import { linkDestination, linkStateLabel, linkTargetName } from "@/features/notes/link-targets";

const ID = "3f2a9c1e-7b4d-4e8a-9c2f-1a2b3c4d5e6f";

const target = (overrides: Partial<LinkTargetDto>): LinkTargetDto => ({
  color: null,
  directoryId: null,
  id: ID,
  isEncrypted: false,
  kind: LinkTargetKind.File,
  mimeType: null,
  name: "Trip.md",
  state: LinkTargetState.Available,
  ...overrides,
});

describe("linkDestination", () => {
  it("opens notes in the editor", () => {
    expect(linkDestination(target({ mimeType: "text/markdown" }))).toEqual({
      to: { name: "note-editor", params: { fileId: ID } },
      type: "route",
    });
  });

  it("shows other files, and encrypted notes, in their folder", () => {
    const folderId = "0b1c2d3e-4f50-4617-8899-aabbccddeeff";

    expect(linkDestination(target({ directoryId: folderId, mimeType: "image/png" }))).toEqual({
      directoryId: folderId,
      fileId: ID,
      type: "reveal",
    });
    expect(
      linkDestination(target({ isEncrypted: true, mimeType: "text/markdown" }))?.type,
    ).toBe("reveal");
  });

  it("routes folders and playlists to their pages", () => {
    expect(linkDestination(target({ kind: LinkTargetKind.Folder }))).toEqual({
      to: { name: "dashboard", params: { dirId: ID } },
      type: "route",
    });
    expect(linkDestination(target({ kind: LinkTargetKind.Playlist }))).toEqual({
      to: { name: "playlist-details", params: { id: ID } },
      type: "route",
    });
  });

  it("sends trashed targets to the trash and leaves unavailable ones unlinked", () => {
    expect(linkDestination(target({ state: LinkTargetState.Trashed }))).toEqual({
      to: { name: "trash" },
      type: "route",
    });
    expect(linkDestination(target({ state: LinkTargetState.Unavailable }))).toBeNull();
  });
});

describe("linkTargetName", () => {
  it("drops a note's extension but keeps other files' names whole", () => {
    expect(linkTargetName(target({ mimeType: "text/markdown" }))).toBe("Trip");
    expect(linkTargetName(target({ mimeType: "application/pdf", name: "Trip.pdf" }))).toBe(
      "Trip.pdf",
    );
    expect(linkTargetName(target({ name: null, state: LinkTargetState.Unavailable }))).toBeNull();
  });
});

describe("linkStateLabel", () => {
  it("names only the states that need saying", () => {
    expect(linkStateLabel(target({}))).toBeNull();
    expect(linkStateLabel(target({ state: LinkTargetState.Trashed }))).toBe("In trash");
    expect(linkStateLabel(target({ state: LinkTargetState.Unavailable }))).toBe("Unavailable");
    expect(linkStateLabel(undefined)).toBeNull();
  });
});
