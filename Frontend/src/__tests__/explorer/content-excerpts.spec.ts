import { describe, expect, it } from "vitest";

import type { ContentMatchSegment } from "@/api/file";

import {
  leadIntoFirstMatch,
  markQueryWords,
  queryWords,
} from "@/features/explorer/content-excerpts";

const text = (isMatch: boolean, value: string): ContentMatchSegment => ({ isMatch, text: value });

const marked = (segments: ContentMatchSegment[]) =>
  segments.filter((segment) => segment.isMatch).map((segment) => segment.text);

const joined = (segments: ContentMatchSegment[]) => segments.map((s) => s.text).join("");

describe("queryWords", () => {
  it("splits on anything that is not a letter or digit, lowercased", () => {
    expect(queryWords("ML-KEM (FIPS 203)")).toEqual(["ml", "kem", "fips", "203"]);
    expect(queryWords("--")).toEqual([]);
  });
});

describe("markQueryWords", () => {
  it("marks the short words the server leaves out", () => {
    const segments = [text(false, "renamed to ML-"), text(true, "KEM"), text(false, " Module")];

    const result = markQueryWords(segments, "ML-KEM");

    expect(marked(result)).toEqual(["ML", "KEM"]);
    expect(joined(result)).toBe("renamed to ML-KEM Module");
  });

  it("marks whole words that start with a typed word, in any case", () => {
    const result = markQueryWords([text(false, "Invoices from paris and PARISIAN hotels")], "pari");

    expect(marked(result)).toEqual(["paris", "PARISIAN"]);
  });

  it("leaves a typed word alone inside another word", () => {
    const result = markQueryWords([text(false, "html and xml files")], "ml");

    expect(marked(result)).toEqual([]);
  });

  it("handles letters outside ASCII", () => {
    const result = markQueryWords([text(false, "Das Café-Menü liegt bei")], "café menü");

    expect(marked(result)).toEqual(["Café", "Menü"]);
  });

  it("keeps the segments as they are without typed words", () => {
    const segments = [text(false, "a"), text(true, "b")];

    expect(markQueryWords(segments, "--")).toBe(segments);
  });
});

describe("leadIntoFirstMatch", () => {
  it("shortens a long lead to its last whole words", () => {
    const segments = [
      text(false, "Algorithm Name Standardized Name Full Expansion CRYSTALS-Kyber renamed to "),
      text(true, "KEM"),
      text(false, " Module"),
    ];

    // The last 28 characters start one letter before "CRYSTALS", so that partial word goes.
    const result = leadIntoFirstMatch(segments, 28);

    expect(joined(result)).toBe("CRYSTALS-Kyber renamed to KEM Module");
    expect(result[1]).toEqual(text(true, "KEM"));
  });

  it("keeps a short lead whole", () => {
    const segments = [text(false, "renamed to "), text(true, "KEM")];

    expect(leadIntoFirstMatch(segments, 24)).toBe(segments);
  });

  it("starts at the match when the lead is a single long word", () => {
    const segments = [text(false, " ".concat("x".repeat(40), " ")), text(true, "KEM")];

    expect(joined(leadIntoFirstMatch(segments, 10))).toBe("KEM");
  });

  it("leaves an excerpt that opens with a match alone", () => {
    const segments = [text(true, "KEM"), text(false, " later")];

    expect(leadIntoFirstMatch(segments, 4)).toBe(segments);
  });
});
