import { describe, expect, it } from "vitest";

import type { GalleryMonth, GalleryPhoto } from "@/api/gallery";

import { GallerySort } from "@/enums/gallery-sort";
import {
  browserCanShowOriginal,
  findPhoto,
  locatePhoto,
  photoDateLabel,
  prefersOriginal,
  stepFrom,
} from "@/features/gallery/viewer";

const photo = (id: string): GalleryPhoto => ({
  fileId: id,
  height: 3000,
  mimeType: "image/jpeg",
  name: `${id}.jpg`,
  takenAt: "2019-06-12T18:15:00.000Z",
  uploadedAt: "2026-11-08T10:00:00.000Z",
  versionId: `v-${id}`,
  width: 4000,
});

const months: GalleryMonth[] = [
  { count: 2, month: 3, year: 2024 },
  { count: 1, month: 1, year: 2024 },
  { count: 2, month: 6, year: 2019 },
];

const allLoaded = new Map([
  ["2024-03", [photo("a"), photo("b")]],
  ["2024-01", [photo("c")]],
  ["2019-06", [photo("d"), photo("e")]],
]);

describe("locatePhoto", () => {
  it("finds a photo's month and place within it", () => {
    expect(locatePhoto(months, allLoaded, "d")).toEqual({ monthIndex: 2, photoIndex: 0 });
  });

  it("returns null when the photo's month is not loaded", () => {
    expect(locatePhoto(months, new Map([["2024-03", [photo("a")]]]), "d")).toBeNull();
  });
});

describe("findPhoto", () => {
  it("returns the loaded photo itself", () => {
    expect(findPhoto(months, allLoaded, "c")?.fileId).toBe("c");
    expect(findPhoto(months, allLoaded, "missing")).toBeNull();
  });
});

describe("stepFrom", () => {
  it("moves within a month", () => {
    const step = stepFrom(months, allLoaded, { monthIndex: 0, photoIndex: 0 }, 1);

    expect(step).toEqual({ kind: "photo", photo: photo("b") });
  });

  it("crosses into the next month's first photo and the previous month's last", () => {
    const forward = stepFrom(months, allLoaded, { monthIndex: 0, photoIndex: 1 }, 1);
    const back = stepFrom(months, allLoaded, { monthIndex: 2, photoIndex: 0 }, -1);

    expect(forward).toEqual({ kind: "photo", photo: photo("c") });
    expect(back).toEqual({ kind: "photo", photo: photo("c") });
  });

  it("asks for a neighbouring month that has not loaded", () => {
    const loaded = new Map([["2024-03", [photo("a"), photo("b")]]]);

    const step = stepFrom(months, loaded, { monthIndex: 0, photoIndex: 1 }, 1);

    expect(step).toEqual({ kind: "load", month: months[1] });
  });

  it("passes over a month left empty since the timeline loaded", () => {
    const loaded = new Map([
      ["2024-03", [photo("a")]],
      ["2024-01", []],
      ["2019-06", [photo("d")]],
    ]);

    const step = stepFrom(months, loaded, { monthIndex: 0, photoIndex: 0 }, 1);

    expect(step).toEqual({ kind: "photo", photo: photo("d") });
  });

  it("stops at both ends of the timeline", () => {
    expect(stepFrom(months, allLoaded, { monthIndex: 0, photoIndex: 0 }, -1).kind).toBe("end");
    expect(stepFrom(months, allLoaded, { monthIndex: 2, photoIndex: 1 }, 1).kind).toBe("end");
  });
});

describe("photoDateLabel", () => {
  it("reads capture times on the camera's clock regardless of the viewer's zone", () => {
    const label = photoDateLabel(photo("a"), GallerySort.Taken);
    const expected = new Intl.DateTimeFormat(undefined, {
      dateStyle: "medium",
      timeStyle: "short",
      timeZone: "UTC",
    }).format(new Date("2019-06-12T18:15:00.000Z"));

    expect(label).toBe(expected);
  });
});

describe("original image fallback", () => {
  it("only offers originals the browser can draw", () => {
    expect(browserCanShowOriginal("image/jpeg")).toBe(true);
    expect(browserCanShowOriginal("IMAGE/WEBP")).toBe(true);
    expect(browserCanShowOriginal("image/heic")).toBe(false);
    expect(browserCanShowOriginal("image/tiff")).toBe(false);
  });

  it("skips the still preview for animated GIFs", () => {
    expect(prefersOriginal("image/gif")).toBe(true);
    expect(prefersOriginal("image/png")).toBe(false);
  });
});
