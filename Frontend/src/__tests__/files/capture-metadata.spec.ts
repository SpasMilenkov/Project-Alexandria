import { describe, expect, it } from "vitest";

import {
  describeCaptureMoment,
  describeShape,
  focalRangeHint,
  formatAperture,
  formatCamera,
  formatCoordinates,
  formatExposure,
  formatFocalLength,
  formatMegapixels,
  formatOffset,
  isoHint,
  shutterHint,
  summarizeSettings,
} from "@/features/files/capture-metadata";

describe("describeCaptureMoment", () => {
  it("reads the camera clock as stored when there is no offset", () => {
    // 14 Jul 2023 was a Friday.
    const moment = describeCaptureMoment("2023-07-14T14:30:00Z", null);

    expect(moment.headline.toLowerCase()).toContain("afternoon");
    expect(moment.date).toContain("2023");
    expect(moment.time).toMatch(/14:30|02:30/u);
    expect(moment.zoneNote).toBe("Camera clock, time zone unknown");
  });

  it("shifts a true instant into the local time where it was taken", () => {
    const withOffset = describeCaptureMoment("2023-07-14T12:30:00Z", 120);
    const wallClock = describeCaptureMoment("2023-07-14T14:30:00Z", null);

    expect(withOffset.headline).toBe(wallClock.headline);
    expect(withOffset.time).toBe(wallClock.time);
    expect(withOffset.zoneNote).toBe("Local time, UTC+02:00");
  });

  it("names the part of the day from the local hour", () => {
    expect(describeCaptureMoment("2023-07-14T08:00:00Z", null).headline).toMatch(/morning$/u);
    expect(describeCaptureMoment("2023-07-14T19:00:00Z", null).headline).toMatch(/evening$/u);
    expect(describeCaptureMoment("2023-07-14T23:30:00Z", null).headline).toMatch(/night$/u);
    expect(describeCaptureMoment("2023-07-14T03:00:00Z", null).headline).toMatch(/night$/u);
  });

  it("can move the weekday when the offset crosses midnight", () => {
    const utcLate = describeCaptureMoment("2023-07-14T23:00:00Z", null);
    const nextMorningInTokyo = describeCaptureMoment("2023-07-14T23:00:00Z", 540);

    expect(nextMorningInTokyo.headline).not.toBe(utcLate.headline);
    expect(nextMorningInTokyo.headline).toMatch(/morning$/u);
  });
});

describe("formatOffset", () => {
  it("formats positive, negative, fractional and zero offsets", () => {
    expect(formatOffset(0)).toBe("UTC");
    expect(formatOffset(330)).toBe("UTC+05:30");
    expect(formatOffset(-300)).toBe("UTC-05:00");
  });
});

describe("formatCamera", () => {
  it("does not repeat the make when the model already includes it", () => {
    expect(formatCamera("Canon", "Canon EOS R5")).toBe("Canon EOS R5");
  });

  it("joins make and model otherwise", () => {
    expect(formatCamera("Apple", "iPhone 15 Pro")).toBe("Apple iPhone 15 Pro");
  });

  it("falls back to whichever part exists", () => {
    expect(formatCamera(null, "X100V")).toBe("X100V");
    expect(formatCamera("FUJIFILM", "  ")).toBe("FUJIFILM");
    expect(formatCamera(null, null)).toBeNull();
  });
});

describe("exposure values", () => {
  it("shows fast shutters as fractions and slow ones in seconds", () => {
    expect(formatExposure(1 / 125)).toBe("1/125 s");
    expect(formatExposure(0.25)).toBe("1/4 s");
    expect(formatExposure(0.5)).toBe("0.5 s");
    expect(formatExposure(2)).toBe("2 s");
  });

  it("trims trailing zeros", () => {
    expect(formatAperture(1.8)).toBe("f/1.8");
    expect(formatAperture(16)).toBe("f/16");
    expect(formatFocalLength(6.86)).toBe("6.9 mm");
  });

  it("summarizes settings, preferring the full-frame focal length", () => {
    const summary = summarizeSettings({
      fNumber: 1.8,
      exposureSeconds: 1 / 120,
      iso: 200,
      focalLengthMm: 6.86,
      focalLength35mm: 26,
    });

    expect(summary).toBe("f/1.8 · 1/120 s · ISO 200 · 26 mm");
  });

  it("leaves out missing settings", () => {
    const summary = summarizeSettings({
      fNumber: null,
      exposureSeconds: null,
      iso: 800,
      focalLengthMm: 50,
      focalLength35mm: null,
    });

    expect(summary).toBe("ISO 800 · 50 mm");
  });
});

describe("plain-language hints", () => {
  it("describes notable shutter speeds only", () => {
    expect(shutterHint(2)).toBe("Long exposure");
    expect(shutterHint(1 / 15)).toBe("Slow, motion may blur");
    expect(shutterHint(1 / 125)).toBeUndefined();
    expect(shutterHint(1 / 1000)).toBe("Fast, freezes motion");
  });

  it("describes notable ISO values only", () => {
    expect(isoHint(100)).toBe("Good light");
    expect(isoHint(640)).toBeUndefined();
    expect(isoHint(3200)).toBe("Low light");
  });

  it("names the lens range from the full-frame focal length", () => {
    expect(focalRangeHint(13)).toBe("Ultra-wide");
    expect(focalRangeHint(26)).toBe("Wide");
    expect(focalRangeHint(50)).toBe("Standard");
    expect(focalRangeHint(120)).toBe("Telephoto");
  });
});

describe("size and place", () => {
  it("describes the frame shape", () => {
    expect(describeShape(4032, 3024)).toBe("Landscape");
    expect(describeShape(3024, 4032)).toBe("Portrait");
    expect(describeShape(2000, 2000)).toBe("Square");
    expect(describeShape(12000, 3000)).toBe("Panorama");
  });

  it("formats megapixels", () => {
    expect(formatMegapixels(4032, 3024)).toBe("12.2 MP");
  });

  it("formats coordinates with hemispheres", () => {
    expect(formatCoordinates(48.858_37, -2.294_48)).toBe("48.85837° N, 2.29448° W");
    expect(formatCoordinates(-33.8688, 151.2093)).toBe("33.86880° S, 151.20930° E");
  });
});
