import { describe, expect, it } from "vitest";

import type { GalleryMonth, GalleryPhoto } from "@/api/gallery";

import { useGallerySelection } from "@/composables/useGallerySelection";
import {
  monthsHolding,
  photoClick,
  photoRange,
  photosInOrder,
  photosSubject,
} from "@/features/gallery/selection";

const photo = (id: string) => ({ fileId: id, name: `${id}.jpg` }) as GalleryPhoto;

const months: GalleryMonth[] = [
  { count: 2, month: 3, year: 2024 },
  { count: 1, month: 1, year: 2024 },
  { count: 2, month: 6, year: 2019 },
];

const loaded = new Map([
  ["2024-03", [photo("a"), photo("b")]],
  ["2019-06", [photo("d"), photo("e")]],
]);

describe("photosInOrder", () => {
  it("lists loaded photos newest first and skips months not loaded", () => {
    expect(photosInOrder(months, loaded).map((entry) => entry.fileId)).toEqual([
      "a",
      "b",
      "d",
      "e",
    ]);
  });
});

describe("photoRange", () => {
  it("covers both ends whichever way round they are picked", () => {
    const forward = photoRange(months, loaded, "b", "e").map((entry) => entry.fileId);
    const backward = photoRange(months, loaded, "e", "b").map((entry) => entry.fileId);

    expect(forward).toEqual(["b", "d", "e"]);
    expect(backward).toEqual(["b", "d", "e"]);
  });

  it("selects nothing when an end is no longer loaded", () => {
    expect(photoRange(months, loaded, "gone", "e")).toEqual([]);
  });
});

describe("monthsHolding", () => {
  it("returns the months the photos sit in", () => {
    expect(monthsHolding(months, loaded, new Set(["b", "e"]))).toEqual([months[0], months[2]]);
  });
});

describe("photosSubject", () => {
  it("names a single photo and counts several", () => {
    expect(photosSubject([photo("a")])).toBe('"a.jpg"');
    expect(photosSubject([photo("a"), photo("b")])).toBe("2 photos");
  });
});

describe("useGallerySelection", () => {
  it("toggles a photo in and out and remembers it as the anchor", () => {
    const selection = useGallerySelection();

    selection.toggle(photo("a"));
    expect(selection.isSelected("a")).toBe(true);
    expect(selection.anchorId()).toBe("a");

    selection.toggle(photo("a"));
    expect(selection.active.value).toBe(false);
  });

  it("sets many at once without moving the anchor", () => {
    const selection = useGallerySelection();

    selection.toggle(photo("a"));
    selection.setMany([photo("b"), photo("c")], true);

    expect(selection.count.value).toBe(3);
    expect(selection.anchorId()).toBe("a");
  });

  it("forgets removed photos and an anchor that went with them", () => {
    const selection = useGallerySelection();

    selection.setMany([photo("b")], true);
    selection.toggle(photo("a"));
    selection.forget(new Set(["a"]));

    expect(selection.photos.value.map((entry) => entry.fileId)).toEqual(["b"]);
    expect(selection.anchorId()).toBeNull();
  });

  it("narrows to one photo and keeps it as the anchor", () => {
    const selection = useGallerySelection();

    selection.setMany([photo("a"), photo("b")], true);
    selection.selectOnly(photo("c"));

    expect(selection.photos.value.map((entry) => entry.fileId)).toEqual(["c"]);
    expect(selection.anchorId()).toBe("c");
  });

  it("replaces the selection with a range and leaves the anchor alone", () => {
    const selection = useGallerySelection();

    selection.toggle(photo("b"));
    selection.replaceWith([photo("b"), photo("d"), photo("e")]);
    selection.replaceWith([photo("a"), photo("b")]);

    expect(selection.photos.value.map((entry) => entry.fileId)).toEqual(["a", "b"]);
    expect(selection.anchorId()).toBe("b");
  });
});

describe("photoClick", () => {
  const base = { selecting: false, shift: false, toggleKey: false, touch: false };

  it("opens on a plain click when nothing is selected", () => {
    expect(photoClick(base)).toBe("open");
  });

  it("follows the explorer's modifier keys", () => {
    expect(photoClick({ ...base, toggleKey: true })).toBe("toggle");
    expect(photoClick({ ...base, shift: true })).toBe("range");
    expect(photoClick({ ...base, shift: true, toggleKey: true })).toBe("add-range");
  });

  it("narrows on a plain mouse click and toggles on a tap while selecting", () => {
    expect(photoClick({ ...base, selecting: true })).toBe("only");
    expect(photoClick({ ...base, selecting: true, touch: true })).toBe("toggle");
  });
});
