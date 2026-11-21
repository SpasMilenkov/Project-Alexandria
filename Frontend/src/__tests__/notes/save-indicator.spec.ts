import { describe, expect, it } from "vitest";

import { saveIndicatorState } from "@/features/notes/save-indicator";

describe("saveIndicatorState", () => {
  it("says nothing for a note that was opened and not touched", () => {
    expect(saveIndicatorState("idle", false)).toBe("hidden");
  });

  it("shows unsaved edits, then saving, then saved", () => {
    expect(saveIndicatorState("idle", true)).toBe("unsaved");
    expect(saveIndicatorState("saving", true)).toBe("saving");
    expect(saveIndicatorState("saved", false)).toBe("saved");
    expect(saveIndicatorState("saved", true)).toBe("unsaved");
  });

  it("keeps problems visible while typing continues", () => {
    expect(saveIndicatorState("error", true)).toBe("error");
    expect(saveIndicatorState("conflict", true)).toBe("conflict");
  });
});
