import { describe, it, expect } from "vitest";

import { detectLyricsFormat, parseSyncedLyrics } from "@/composables/useLyrics";

const VALID_LRC = ["[00:12.00]First line", "[00:18.50]Second line", "[00:25.10]Third line"].join(
  "\n",
);

describe("detectLyricsFormat", () => {
  it("classifies valid LRC content as synced", () => {
    expect(detectLyricsFormat(VALID_LRC)).toBe("synced");
  });

  it("classifies plain text lyrics as plain", () => {
    const plain = ["First line of the verse", "Second line of the verse"].join("\n");
    expect(detectLyricsFormat(plain)).toBe("plain");
  });

  it("classifies an empty string as plain", () => {
    expect(detectLyricsFormat("")).toBe("plain");
  });

  it("detects LRC with escaped newline sequences as synced", () => {
    const escaped = VALID_LRC.replace(/\n/g, "\\n");
    expect(detectLyricsFormat(escaped)).toBe("synced");
  });

  it("detects LRC with metadata tag headers as synced", () => {
    const withHeaders = ["[ar:Artist]", "[ti:Title]", "", VALID_LRC].join("\n");
    expect(detectLyricsFormat(withHeaders)).toBe("synced");
  });

  it("classifies metadata tags without any time-tagged line as plain", () => {
    const metadataOnly = ["[ar:Artist]", "[ti:Title]", "[by:Someone]"].join("\n");
    expect(detectLyricsFormat(metadataOnly)).toBe("plain");
  });
});

describe("parseSyncedLyrics", () => {
  it("skips metadata header lines while parsing tagged ones", () => {
    const withHeaders = ["[ar:Artist]", "[ti:Title]", "[00:12.00]First line"].join("\n");
    const lines = parseSyncedLyrics(withHeaders);

    expect(lines).toHaveLength(1);
    expect(lines[0]).toEqual({ time: 12, text: "First line" });
  });
});
