import { describe, expect, it } from "vitest";

import {
  isPastPreview,
  momentLabel,
  momentOfMime,
  momentStep,
  normalizeTypedSuffix,
  readMomentInput,
  splitPickerQuery,
} from "@/features/notes/link-picker";
import { PREVIEW_PAGE_LIMIT } from "@/utils/files/mime-catalog";

describe("momentOfMime", () => {
  it("points recordings at a time and PDFs at a page", () => {
    expect(momentOfMime("video/mp4")).toBe("time");
    expect(momentOfMime("audio/mpeg")).toBe("time");
    expect(momentOfMime("application/pdf")).toBe("page");
    expect(momentOfMime("image/png")).toBeNull();
    expect(momentOfMime(null)).toBeNull();
  });

  it("gives documents and slides pages, and spreadsheets none", () => {
    expect(momentOfMime("application/vnd.openxmlformats-officedocument.wordprocessingml.document")).toBe(
      "page",
    );
    expect(momentOfMime("application/vnd.ms-powerpoint")).toBe("page");
    expect(momentOfMime("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet")).toBeNull();
    expect(momentOfMime("text/csv")).toBeNull();
  });
});

describe("isPastPreview", () => {
  it("flags pages the preview doesn't keep", () => {
    expect(isPastPreview(`#page=${PREVIEW_PAGE_LIMIT}`)).toBe(false);
    expect(isPastPreview(`#page=${PREVIEW_PAGE_LIMIT + 1}`)).toBe(true);
    expect(isPastPreview("?t=90")).toBe(false);
  });
});

describe("splitPickerQuery", () => {
  it("searches only the name in front of a typed suffix", () => {
    expect(splitPickerQuery("Holiday clip?t=1:30")).toEqual({
      name: "Holiday clip",
      typedSuffix: "?t=1:30",
    });
    expect(splitPickerQuery("Report #page=4")).toEqual({ name: "Report", typedSuffix: "#page=4" });
    expect(splitPickerQuery("Holiday")).toEqual({ name: "Holiday", typedSuffix: null });
  });
});

describe("normalizeTypedSuffix", () => {
  it("writes typed moments the way links are saved, only on files they fit", () => {
    expect(normalizeTypedSuffix("time", "?t=1:30")).toBe("?t=90");
    expect(normalizeTypedSuffix("page", "#page=4")).toBe("#page=4");
    expect(normalizeTypedSuffix("page", "?t=1:30")).toBeNull();
    expect(normalizeTypedSuffix(null, "?t=1:30")).toBeNull();
  });
});

describe("readMomentInput", () => {
  it("reads every way people write a start time", () => {
    for (const typed of ["90", "1:30", "1m30s", "?t=1:30", "@1:30", "at 1:30"]) {
      expect(readMomentInput("time", typed, null)).toEqual({ state: "valid", suffix: "?t=90" });
    }
  });

  it("takes the playing position for now, only while the file plays", () => {
    expect(readMomentInput("time", "now", 192)).toEqual({ state: "valid", suffix: "?t=192" });
    expect(readMomentInput("time", "now", null)).toEqual({
      reason: "unreadable",
      state: "invalid",
      text: "now",
    });
  });

  it("reads pages and rejects page zero", () => {
    expect(readMomentInput("page", "4", null)).toEqual({ state: "valid", suffix: "#page=4" });
    expect(readMomentInput("page", "p. 4", null)).toEqual({ state: "valid", suffix: "#page=4" });
    expect(readMomentInput("page", "0", null)).toEqual({
      reason: "unreadable",
      state: "invalid",
      text: "0",
    });
  });

  it("stops at the end of what it links into", () => {
    expect(readMomentInput("time", "3:11", null, 192)).toEqual({ state: "valid", suffix: "?t=191" });
    expect(readMomentInput("time", "3:12", null, 192)).toMatchObject({ reason: "too-far" });
    expect(readMomentInput("page", "24", null, 24)).toEqual({ state: "valid", suffix: "#page=24" });
    expect(readMomentInput("page", "25", null, 24)).toMatchObject({ reason: "too-far" });
  });

  it("treats nothing typed as the start", () => {
    expect(readMomentInput("time", "  ", null)).toEqual({ state: "empty" });
  });
});

describe("momentStep", () => {
  it("blocks picking until the typed text is a time", () => {
    const step = momentStep("time", { reason: "unreadable", state: "invalid", text: "soon" }, null);

    expect(step.blocked).toBe(true);
    expect(step.description).toBe('"soon" isn\'t a time');
  });

  it("says how long the file runs and when a moment is past it", () => {
    const tooLate = momentStep("time", { reason: "too-far", state: "invalid", text: "9:00" }, null, 192);

    expect(tooLate.blocked).toBe(true);
    expect(tooLate.description).toBe('"9:00" is past the end (3:12)');
    expect(tooLate.hint.startsWith("Runs 3:12.")).toBe(true);
    expect(momentStep("page", { state: "empty" }, null, 5).hint).toMatch(/^The preview has 5 pages\./u);
  });

  it("names the moment it will insert", () => {
    expect(momentStep("time", { state: "valid", suffix: "?t=90" }, null).description).toBe(
      "Starts at 1:30",
    );
    expect(momentStep("page", { state: "valid", suffix: "#page=4" }, null).description).toBe(
      "Opens at page 4",
    );
  });

  it("offers now in the hint only while the file plays", () => {
    expect(momentStep("time", { state: "empty" }, 192).hint).toContain('"now" for 3:12');
    expect(momentStep("time", { state: "empty" }, null).hint).not.toContain("now");
  });
});

describe("momentLabel", () => {
  it("describes times and pages", () => {
    expect(momentLabel("?t=90")).toBe("at 1:30");
    expect(momentLabel("#page=2")).toBe("page 2");
    expect(momentLabel("")).toBeNull();
  });
});
