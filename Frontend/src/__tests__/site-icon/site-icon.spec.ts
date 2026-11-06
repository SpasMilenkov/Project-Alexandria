import { createPinia, setActivePinia } from "pinia";
import { beforeEach, describe, expect, it } from "vitest";

import { ACTIVITY_GLYPHS, PLACE_GLYPHS } from "@/features/site-icon/places";
import { buildSiteIconSvg } from "@/features/site-icon/render";
import {
  DOT_COLORS,
  type SiteIconInput,
  accentTile,
  resolveSiteIcon,
  stepProgress,
} from "@/features/site-icon/state";
import appRouter from "@/router";
import { useUploadActivityStore } from "@/stores/upload-activity";

const AMBER = "245 158 11";
const GLYPH = { body: '<path fill="currentColor" d="M0 0h24v24H0z"/>', height: 24, width: 24 };

const input = (overrides: Partial<SiteIconInput> = {}): SiteIconInput => ({
  accentRgb: AMBER,
  health: null,
  isOnline: true,
  pageTitle: "Files",
  place: "files",
  playback: null,
  upload: null,
  uploadOutcome: null,
  ...overrides,
});

const uploading = { fileCount: 4, kind: "upload" as const, label: null, progress: 38 };

describe("resolveSiteIcon", () => {
  it("shows the page glyph on the accent tile when idle", () => {
    const icon = resolveSiteIcon(input());

    expect(icon.glyph).toBe(PLACE_GLYPHS.files);
    expect(icon.tile).toBe(accentTile(AMBER));
    expect(icon.fill).toBeNull();
    expect(icon.dot).toBeNull();
    expect(icon.title).toBe("Files · Alexandria");
  });

  it("keeps the app name alone instead of repeating it", () => {
    expect(resolveSiteIcon(input({ pageTitle: "Alexandria" })).title).toBe("Alexandria");
  });

  it("turns the tile gray only when offline", () => {
    const offline = resolveSiteIcon(input({ isOnline: false }));
    const failed = resolveSiteIcon(input({ uploadOutcome: "failed" }));

    expect(offline.tile).not.toBe(accentTile(AMBER));
    expect(offline.title).toBe("Offline · Files");
    expect(failed.tile).toBe(accentTile(AMBER));
  });

  it("marks a failed upload with the warning glyph and a red dot", () => {
    const icon = resolveSiteIcon(input({ upload: uploading, uploadOutcome: "failed" }));

    expect(icon.glyph).toBe(ACTIVITY_GLYPHS.failed);
    expect(icon.dot).toBe(DOT_COLORS.down);
    expect(icon.title).toBe("Upload failed · Files");
  });

  it("lets an upload take the glyph and fill over playback", () => {
    const icon = resolveSiteIcon(input({ playback: { title: "Teardrop" }, upload: uploading }));

    expect(icon.glyph).toBe(ACTIVITY_GLYPHS.upload);
    expect(icon.fill).toBe(3 / 8);
    expect(icon.title).toBe("↑ 38% · Uploading 4 files");
  });

  it("names the subject of an extraction", () => {
    const icon = resolveSiteIcon(
      input({ upload: { ...uploading, kind: "extract", label: "photos.zip", progress: 70 } }),
    );

    expect(icon.glyph).toBe(ACTIVITY_GLYPHS.extract);
    expect(icon.title).toBe("70% · Extracting photos.zip");
  });

  it("shows the speaker and track title on any page while playing", () => {
    const playing = resolveSiteIcon(
      input({ place: "settings", playback: { title: "Teardrop · Massive Attack" } }),
    );

    expect(playing.glyph).toBe(ACTIVITY_GLYPHS.playing);
    expect(playing.title).toBe("Teardrop · Massive Attack");
  });

  it("ranks the corner dot: down, then degraded, then done", () => {
    const down = resolveSiteIcon(
      input({ health: { degraded: 1, down: 2 }, uploadOutcome: "done" }),
    );
    const degraded = resolveSiteIcon(
      input({ health: { degraded: 1, down: 0 }, uploadOutcome: "done" }),
    );
    const done = resolveSiteIcon(input({ uploadOutcome: "done" }));

    expect(down.dot).toBe(DOT_COLORS.down);
    expect(down.title).toBe("2 services down · Files");
    expect(degraded.dot).toBe(DOT_COLORS.degraded);
    expect(degraded.title).toBe("1 service degraded · Files");
    expect(done.dot).toBe(DOT_COLORS.done);
    expect(done.title).toBe("✓ Upload finished · Files");
  });
});

describe("stepProgress", () => {
  it("moves in eighths without running ahead or showing an empty fill", () => {
    expect(stepProgress(0)).toBe(1 / 8);
    expect(stepProgress(24)).toBe(1 / 8);
    expect(stepProgress(25)).toBe(2 / 8);
    expect(stepProgress(99)).toBe(7 / 8);
    expect(stepProgress(100)).toBe(1);
    expect(stepProgress(140)).toBe(1);
  });
});

describe("accentTile", () => {
  it("darkens the accent slightly and accepts comma or space separators", () => {
    expect(accentTile("245 158 11")).toBe("#d88b0a");
    expect(accentTile("245,158,11")).toBe("#d88b0a");
  });
});

describe("buildSiteIconSvg", () => {
  it("cuts the tile out under the dot only when there is one", () => {
    const plain = buildSiteIconSvg(resolveSiteIcon(input()), GLYPH);
    const dotted = buildSiteIconSvg(resolveSiteIcon(input({ uploadOutcome: "done" })), GLYPH);

    expect(plain).not.toContain("<mask");
    expect(dotted).toContain('mask="url(#d)"');
    expect(dotted).toContain(`fill="${DOT_COLORS.done}"`);
  });

  it("draws the fill level from the bottom", () => {
    const svg = buildSiteIconSvg(
      resolveSiteIcon(input({ upload: { ...uploading, progress: 50 } })),
      GLYPH,
    );

    expect(svg).toContain('<rect y="16" width="32" height="16"');
  });

  it("inlines white so the glyph survives rasterizers without currentColor", () => {
    const svg = buildSiteIconSvg(resolveSiteIcon(input()), GLYPH);

    expect(svg).not.toContain("currentColor");
    expect(svg).toContain('fill="#fff"');
  });
});

describe("route metadata", () => {
  it("gives every named route a glyph and a title", () => {
    const missing = appRouter
      .getRoutes()
      .filter((route) => route.name && (!route.meta.siteIcon || !route.meta.title))
      .map((route) => String(route.name));

    expect(missing).toEqual([]);
  });
});

describe("useUploadActivityStore", () => {
  beforeEach(() => {
    setActivePinia(createPinia());
  });

  it("weights combined progress by file count", () => {
    const store = useUploadActivityStore();

    store.report("a", { fileCount: 3, kind: "upload", label: "Photos", progress: 100 });
    store.report("b", { fileCount: 1, kind: "upload", label: "notes.txt", progress: 0 });

    expect(store.current).toEqual({ fileCount: 4, kind: "upload", label: null, progress: 75 });
  });

  it("records outcomes only for uploads that ended in the background", () => {
    const store = useUploadActivityStore();

    store.report("a", uploading);
    store.settle("a", "done", false);
    expect(store.outcome).toBeNull();
    expect(store.current).toBeNull();

    store.report("b", uploading);
    store.settle("b", "done", true);
    expect(store.outcome).toBe("done");
  });

  it("keeps a failure until cleared, even if a later upload succeeds", () => {
    const store = useUploadActivityStore();

    store.settle("a", "failed", true);
    store.settle("b", "done", true);
    expect(store.outcome).toBe("failed");

    store.clearOutcome();
    expect(store.outcome).toBeNull();
  });
});
