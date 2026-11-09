import { describe, expect, it } from "vitest";

import { GallerySort } from "@/enums/gallery-sort";
import { monthBounds, monthKey, monthLabel } from "@/features/gallery/months";

describe("monthBounds", () => {
  it("uses UTC months for capture dates, which are stored as wall clock labeled UTC", () => {
    expect(monthBounds(GallerySort.Taken, 2019, 6)).toEqual({
      before: "2019-07-01T00:00:00.000Z",
      since: "2019-06-01T00:00:00.000Z",
    });
  });

  it("rolls December over into the next year", () => {
    expect(monthBounds(GallerySort.Taken, 2023, 12).before).toBe("2024-01-01T00:00:00.000Z");
  });

  it("uses the viewer's local months for upload dates", () => {
    const { before, since } = monthBounds(GallerySort.Uploaded, 2024, 2);

    expect(new Date(since).getFullYear()).toBe(2024);
    expect(new Date(since).getMonth()).toBe(1);
    expect(new Date(since).getDate()).toBe(1);
    expect(new Date(since).getHours()).toBe(0);
    expect(new Date(before).getMonth()).toBe(2);
  });
});

describe("month labels", () => {
  it("pads keys so they sort and match the server's months", () => {
    expect(monthKey(2019, 6)).toBe("2019-06");
  });

  it("names the month without shifting it through the local zone", () => {
    expect(monthLabel(2019, 1)).toMatch(/2019/u);
    expect(monthLabel(2019, 1)).not.toMatch(/2018/u);
  });
});
