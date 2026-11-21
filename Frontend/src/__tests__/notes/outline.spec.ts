import { describe, expect, it } from "vitest";

import { extractHeadings, headingAtLine, lineAtOffset } from "@/features/notes/outline";

describe("extractHeadings", () => {
  it("reads ATX headings with their level and line", () => {
    expect(extractHeadings("# Trip\n\nText\n\n## Day 1\n### Food")).toEqual([
      { level: 1, line: 0, text: "Trip" },
      { level: 2, line: 4, text: "Day 1" },
      { level: 3, line: 5, text: "Food" },
    ]);
  });

  it("drops closing hashes and inline syntax from the label", () => {
    expect(extractHeadings("## **Bold** and `code` [link](http://x) ##")).toEqual([
      { level: 2, line: 0, text: "Bold and code link" },
    ]);
  });

  it("keeps an empty heading so entries line up with the rendered headings", () => {
    expect(extractHeadings("#\n\n# Next")).toEqual([
      { level: 1, line: 0, text: "" },
      { level: 1, line: 2, text: "Next" },
    ]);
  });

  it("ignores headings inside fenced code", () => {
    expect(extractHeadings("```md\n# not a heading\n```\n# Real")).toEqual([
      { level: 1, line: 3, text: "Real" },
    ]);
  });

  it("ignores headings inside quotes and lists", () => {
    expect(extractHeadings("> # quoted\n\n- # listed\n\n# Top")).toEqual([
      { level: 1, line: 4, text: "Top" },
    ]);
  });

  it("reads setext headings from their first line, including ones over several lines", () => {
    expect(extractHeadings("Title\n=====\n\nTwo\nlines\n---")).toEqual([
      { level: 1, line: 0, text: "Title" },
      { level: 2, line: 3, text: "Two lines" },
    ]);
  });

  it("treats --- after a blank line as a divider, not a heading", () => {
    expect(extractHeadings("Text\n\n---\n\nMore")).toEqual([]);
  });

  it("requires a space after the hashes", () => {
    expect(extractHeadings("#hashtag\n#\tTabbed")).toEqual([
      { level: 1, line: 1, text: "Tabbed" },
    ]);
  });
});

describe("lineAtOffset", () => {
  it("counts the newlines before the offset", () => {
    const text = "one\ntwo\nthree";

    expect(lineAtOffset(text, 0)).toBe(0);
    expect(lineAtOffset(text, 3)).toBe(0);
    expect(lineAtOffset(text, 4)).toBe(1);
    expect(lineAtOffset(text, text.length)).toBe(2);
  });
});

describe("headingAtLine", () => {
  const headings = extractHeadings("Intro\n\n# One\ntext\n\n## Two\ntext");

  it("is null above the first heading", () => {
    expect(headingAtLine(headings, 0)).toBeNull();
  });

  it("picks the last heading at or above the line", () => {
    expect(headingAtLine(headings, 2)).toBe(0);
    expect(headingAtLine(headings, 4)).toBe(0);
    expect(headingAtLine(headings, 6)).toBe(1);
  });
});
