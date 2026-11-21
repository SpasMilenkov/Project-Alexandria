import { describe, expect, it } from "vitest";

import { newNoteFolderFor } from "@/features/notes/new-note";

describe("newNoteFolderFor", () => {
  it("uses the folder being browsed when it is inside Notes", () => {
    const path = [{ id: "notes" }, { id: "projects" }, { id: "alexandria" }];

    expect(newNoteFolderFor(path, "notes")).toBe("alexandria");
  });

  it("uses the Notes folder itself while browsing it", () => {
    expect(newNoteFolderFor([{ id: "notes" }], "notes")).toBe("notes");
  });

  it("leaves the choice to the server outside Notes, at the root or before Notes exists", () => {
    expect(newNoteFolderFor([{ id: "music" }, { id: "albums" }], "notes")).toBeUndefined();
    expect(newNoteFolderFor([], "notes")).toBeUndefined();
    expect(newNoteFolderFor(undefined, "notes")).toBeUndefined();
    expect(newNoteFolderFor([{ id: "notes" }], null)).toBeUndefined();
  });
});
