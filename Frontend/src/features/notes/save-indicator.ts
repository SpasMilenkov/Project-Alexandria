import type { NoteSaveStatus } from "@/composables/useNoteSave";

export type SaveIndicatorState = "hidden" | "unsaved" | "saving" | "saved" | "error" | "conflict";

/**
 * What the editor header says about saving. Problems outrank everything, so a failed or blocked
 * save stays visible while typing continues; a clean note that was never saved says nothing.
 */
export const saveIndicatorState = (status: NoteSaveStatus, isDirty: boolean): SaveIndicatorState => {
  if (status === "conflict") return "conflict";
  if (status === "error") return "error";
  if (status === "saving") return "saving";
  if (isDirty) return "unsaved";
  if (status === "saved") return "saved";

  return "hidden";
};
