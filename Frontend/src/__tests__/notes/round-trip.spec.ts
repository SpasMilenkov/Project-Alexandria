import { describe, expect, it } from "vitest";

import { normalizeMarkdown, survivesRoundTrip } from "@/features/notes/round-trip";

describe("normalizeMarkdown", () => {
  it("turns Windows and old Mac line endings into newlines", () => {
    expect(normalizeMarkdown("# Title\r\n\r\nBody\rMore")).toBe("# Title\n\nBody\nMore");
  });

  it("strips a single trailing space and trailing tabs", () => {
    expect(normalizeMarkdown("one \ntwo\t\nthree")).toBe("one\ntwo\nthree");
  });

  it("keeps two or more trailing spaces as one hard break marker", () => {
    expect(normalizeMarkdown("first    \nsecond")).toBe("first  \nsecond");
  });

  it("drops blank lines around the document but keeps the ones inside it", () => {
    expect(normalizeMarkdown("\n\n# Title\n\n\n\nBody\n\n")).toBe("# Title\n\n\n\nBody");
  });
});

describe("survivesRoundTrip", () => {
  it("accepts output that only differs in whitespace that never renders", () => {
    expect(survivesRoundTrip("# Title\r\n\r\nBody \r\n", "# Title\n\nBody")).toBe(true);
  });

  it("rejects a changed list marker", () => {
    expect(survivesRoundTrip("* one\n* two", "- one\n- two")).toBe(false);
  });

  it("rejects a dropped hard break", () => {
    expect(survivesRoundTrip("first  \nsecond", "first\nsecond")).toBe(false);
  });

  it("rejects collapsed blank lines", () => {
    expect(survivesRoundTrip("one\n\n\ntwo", "one\n\ntwo")).toBe(false);
  });

  it("accepts two empty documents", () => {
    expect(survivesRoundTrip("", "\n")).toBe(true);
  });
});
