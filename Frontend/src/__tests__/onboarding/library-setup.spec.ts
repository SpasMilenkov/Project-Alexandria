import { describe, expect, it } from "vitest";

import { AudioRung, VideoRung } from "@/api/policy";
import { LibraryFolderKind } from "@/enums";
import {
  type LibraryCapabilities,
  type LibraryFolderDraft,
  buildFinishTourRequest,
  createLibraryDrafts,
  hasAnyAutomation,
  isDraftAutomated,
  validateFolderNames,
  validateQualities,
} from "@/features/onboarding/library-setup";

const everything: LibraryCapabilities = { autoTagging: true, streaming: true };
const nothing: LibraryCapabilities = { autoTagging: false, streaming: false };

const draftOf = (drafts: LibraryFolderDraft[], kind: LibraryFolderKind) => {
  const draft = drafts.find((d) => d.kind === kind);
  if (!draft) throw new Error(`missing draft ${kind}`);

  return draft;
};

describe("createLibraryDrafts", () => {
  it("offers all five folders with default names", () => {
    const drafts = createLibraryDrafts(everything);

    expect(drafts.map((d) => d.name)).toEqual([
      "Music",
      "Movies",
      "Pictures",
      "Videos",
      "Documents",
    ]);
  });

  it("turns automation on only for folders the server can automate", () => {
    const drafts = createLibraryDrafts(everything);

    expect(draftOf(drafts, LibraryFolderKind.Music).automationEnabled).toBe(true);
    expect(draftOf(drafts, LibraryFolderKind.Movies).automationEnabled).toBe(true);
    expect(draftOf(drafts, LibraryFolderKind.Pictures).automationEnabled).toBe(false);
    expect(draftOf(drafts, LibraryFolderKind.Videos).automationEnabled).toBe(false);
  });

  it("leaves Music automated through tagging when streaming is off", () => {
    const drafts = createLibraryDrafts({ autoTagging: true, streaming: false });

    expect(draftOf(drafts, LibraryFolderKind.Music).automationEnabled).toBe(true);
    expect(draftOf(drafts, LibraryFolderKind.Movies).automationEnabled).toBe(false);
  });
});

describe("hasAnyAutomation", () => {
  it("is false when the server can run nothing", () => {
    expect(hasAnyAutomation(nothing)).toBe(false);
    expect(hasAnyAutomation({ autoTagging: false, streaming: true })).toBe(true);
  });
});

describe("validateFolderNames", () => {
  it("flags empty, invalid and duplicate names", () => {
    const drafts = createLibraryDrafts(everything);
    draftOf(drafts, LibraryFolderKind.Music).name = "   ";
    draftOf(drafts, LibraryFolderKind.Movies).name = "Films/TV";
    draftOf(drafts, LibraryFolderKind.Documents).name = "pictures";

    const errors = validateFolderNames(drafts);

    expect(Object.keys(errors).map(Number).sort()).toEqual([
      LibraryFolderKind.Music,
      LibraryFolderKind.Movies,
      LibraryFolderKind.Documents,
    ]);
    expect(errors[LibraryFolderKind.Documents]).toMatch(/already uses/u);
  });

  it("accepts the defaults", () => {
    expect(validateFolderNames(createLibraryDrafts(everything))).toEqual({});
  });
});

describe("validateQualities", () => {
  it("asks for a quality when transcoding is on with none picked", () => {
    const drafts = createLibraryDrafts(everything);
    draftOf(drafts, LibraryFolderKind.Movies).videoRungs = [];

    expect(Object.keys(validateQualities(drafts, everything))).toEqual([
      String(LibraryFolderKind.Movies),
    ]);
  });

  it("ignores folders with automation turned off", () => {
    const drafts = createLibraryDrafts(everything);
    const movies = draftOf(drafts, LibraryFolderKind.Movies);
    movies.videoRungs = [];
    movies.automationEnabled = false;

    expect(validateQualities(drafts, everything)).toEqual({});
  });

  it("lets music skip every quality while tagging stays on", () => {
    const drafts = createLibraryDrafts(everything);
    draftOf(drafts, LibraryFolderKind.Music).audioRungs = [];

    expect(validateQualities(drafts, everything)).toEqual({});
  });

  it("asks for a quality when music would end up with no rule at all", () => {
    const drafts = createLibraryDrafts(everything);
    const music = draftOf(drafts, LibraryFolderKind.Music);
    music.audioRungs = [];
    music.autoTag = false;

    expect(Object.keys(validateQualities(drafts, everything))).toEqual([
      String(LibraryFolderKind.Music),
    ]);
  });
});

describe("isDraftAutomated", () => {
  it("is false when the switch is on but nothing would run", () => {
    const capabilities: LibraryCapabilities = { autoTagging: true, streaming: false };
    const drafts = createLibraryDrafts(capabilities);
    const music = draftOf(drafts, LibraryFolderKind.Music);
    music.autoTag = false;

    expect(music.automationEnabled).toBe(true);
    expect(isDraftAutomated(music, capabilities)).toBe(false);
  });

  it("is true for tagging alone", () => {
    const drafts = createLibraryDrafts(everything);
    const music = draftOf(drafts, LibraryFolderKind.Music);
    music.audioRungs = [];

    expect(isDraftAutomated(music, everything)).toBe(true);
  });
});

describe("buildFinishTourRequest", () => {
  it("sends trimmed names and only the qualities each folder uses", () => {
    const drafts = createLibraryDrafts(everything);
    draftOf(drafts, LibraryFolderKind.Music).name = "  My Music ";

    const { folders } = buildFinishTourRequest(drafts, everything);
    const music = folders.find((f) => f.kind === LibraryFolderKind.Music);
    const movies = folders.find((f) => f.kind === LibraryFolderKind.Movies);

    expect(music).toEqual({
      audioRungs: [AudioRung.Kbps192, AudioRung.Kbps320],
      autoTag: true,
      automationEnabled: true,
      kind: LibraryFolderKind.Music,
      name: "My Music",
      videoRungs: [],
    });
    expect(movies?.videoRungs).toEqual([VideoRung.P720, VideoRung.P1080]);
    expect(movies?.audioRungs).toEqual([]);
  });

  it("strips everything the server cannot run", () => {
    const drafts = createLibraryDrafts(everything);

    const { folders } = buildFinishTourRequest(drafts, nothing);

    expect(folders).toHaveLength(5);
    expect(folders.every((f) => !f.automationEnabled && !f.autoTag)).toBe(true);
    expect(folders.every((f) => f.audioRungs.length === 0 && f.videoRungs.length === 0)).toBe(true);
  });

  it("keeps a folder with automation off as a plain folder", () => {
    const drafts = createLibraryDrafts(everything);
    draftOf(drafts, LibraryFolderKind.Music).automationEnabled = false;

    const music = buildFinishTourRequest(drafts, everything).folders[0];

    expect(music.automationEnabled).toBe(false);
    expect(music.autoTag).toBe(false);
    expect(music.audioRungs).toEqual([]);
  });

  it("sends a tag-only folder without qualities", () => {
    const drafts = createLibraryDrafts(everything);
    draftOf(drafts, LibraryFolderKind.Music).audioRungs = [];

    const music = buildFinishTourRequest(drafts, everything).folders[0];

    expect(music.automationEnabled).toBe(true);
    expect(music.autoTag).toBe(true);
    expect(music.audioRungs).toEqual([]);
  });

  it("marks a folder with nothing selected as plain", () => {
    const capabilities: LibraryCapabilities = { autoTagging: true, streaming: false };
    const drafts = createLibraryDrafts(capabilities);
    draftOf(drafts, LibraryFolderKind.Music).autoTag = false;

    const music = buildFinishTourRequest(drafts, capabilities).folders[0];

    expect(music.automationEnabled).toBe(false);
    expect(music.autoTag).toBe(false);
  });
});
