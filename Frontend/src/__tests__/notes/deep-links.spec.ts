import { describe, expect, it } from "vitest";

import {
  atPdfPage,
  deepLinkLabel,
  pdfPageOf,
  startSecondsOf,
  startSuffix,
} from "@/features/notes/deep-links";

describe("startSecondsOf", () => {
  it.each([
    ["?t=90", 90],
    ["?t=90s", 90],
    ["?t=1m30s", 90],
    ["?t=1h2m3s", 3723],
    ["?t=2h", 7200],
    ["?t=1:30", 90],
    ["?t=01:02:03", 3723],
    ["?t=0", 0],
    ["?t=724#page=2", 724],
    ["?x=1&t=5", 5],
  ])("reads %s as %i seconds", (suffix, seconds) => {
    expect(startSecondsOf(suffix)).toBe(seconds);
  });

  it.each(["", "#t=90", "?t=", "?t=soon", "?t=1:5", "?t=-3", "?t=99999999"])(
    "finds no time in %j",
    (suffix) => {
      expect(startSecondsOf(suffix)).toBeNull();
    },
  );
});

describe("pdfPageOf", () => {
  it.each([
    ["#page=3", 3],
    ["?t=9#page=12", 12],
    ["#zoom=2&page=4", 4],
  ])("reads %s as page %i", (suffix, page) => {
    expect(pdfPageOf(suffix)).toBe(page);
  });

  it.each(["", "?page=3", "#page=0", "#page=x", "#mypage=3"])("finds no page in %j", (suffix) => {
    expect(pdfPageOf(suffix)).toBeNull();
  });
});

describe("startSuffix", () => {
  it("writes whole seconds, never below zero", () => {
    expect(startSuffix(724.8)).toBe("?t=724");
    expect(startSuffix(-2)).toBe("?t=0");
  });
});

describe("deepLinkLabel", () => {
  it("shows a time as the player does", () => {
    expect(deepLinkLabel("?t=724")).toBe("12:04");
    expect(deepLinkLabel("?t=3723")).toBe("1:02:03");
  });

  it("shows a page", () => {
    expect(deepLinkLabel("#page=3")).toBe("p. 3");
  });

  it("shows nothing for a plain link", () => {
    expect(deepLinkLabel("")).toBeNull();
  });
});

describe("atPdfPage", () => {
  it("replaces any fragment with the page", () => {
    expect(atPdfPage("https://s3/doc.pdf?sig=1#toolbar=0", 4)).toBe("https://s3/doc.pdf?sig=1#page=4");
  });

  it("leaves the URL alone without a page", () => {
    expect(atPdfPage("https://s3/doc.pdf", null)).toBe("https://s3/doc.pdf");
  });
});
