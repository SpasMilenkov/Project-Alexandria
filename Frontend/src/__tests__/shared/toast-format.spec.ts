import { describe, expect, it } from "vitest";

import { countOf, describeExplorerItems, quoted } from "@/features/toast/format";

describe("toast wording", () => {
  it("pluralizes counts", () => {
    expect(countOf(1, "file")).toBe("1 file");
    expect(countOf(3, "file")).toBe("3 files");
    expect(countOf(2, "directory", "directories")).toBe("2 directories");
  });

  it("names mixed explorer selections", () => {
    expect(describeExplorerItems(3, 1)).toBe("3 files and 1 folder");
    expect(describeExplorerItems(0, 2)).toBe("2 folders");
    expect(describeExplorerItems(1, 0)).toBe("1 file");
  });

  it("quotes item names", () => {
    expect(quoted("Road trip")).toBe('"Road trip"');
  });
});
