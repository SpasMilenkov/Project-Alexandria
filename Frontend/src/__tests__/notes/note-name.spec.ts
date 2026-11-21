import { describe, expect, it } from "vitest";

import { noteTitleOf } from "@/features/notes/note-name";

describe("noteTitleOf", () => {
  it("drops the markdown extension, whatever its case", () => {
    expect(noteTitleOf("Trip plans.md")).toBe("Trip plans");
    expect(noteTitleOf("Trip plans.MD")).toBe("Trip plans");
    expect(noteTitleOf("Trip plans.markdown")).toBe("Trip plans");
  });

  it("keeps dots that are part of the name", () => {
    expect(noteTitleOf("v1.2 release notes.md")).toBe("v1.2 release notes");
    expect(noteTitleOf("notes.txt")).toBe("notes.txt");
  });
});
