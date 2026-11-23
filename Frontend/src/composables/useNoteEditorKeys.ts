import { useEventListener } from "@vueuse/core";
import { type MaybeRefOrGetter, toValue } from "vue";

interface NoteEditorKeys {
  // Keys only act while a note is open for editing, not over a loading or blocked page.
  isEditing: MaybeRefOrGetter<boolean>;
  isZen: MaybeRefOrGetter<boolean>;
  saveVersion: () => void;
  toggleZen: () => void;
  exitZen: () => void;
}

const isPlainMod = (event: KeyboardEvent) =>
  (event.ctrlKey || event.metaKey) && !event.altKey && !event.shiftKey;

const isShiftMod = (event: KeyboardEvent) =>
  (event.ctrlKey || event.metaKey) && !event.altKey && event.shiftKey;

// Escape belongs to whatever is open first. The link picker and the rename field claim it, and
// menus, dialogs and tooltips close on the same keypress without claiming it, so an open one of
// those counts as taken too. Zen only ends once nothing else is there to close.
const escapeIsTaken = (event: KeyboardEvent) =>
  event.defaultPrevented || document.querySelector("[data-dismissable-layer]") !== null;

/** The note editor's own shortcuts: Ctrl+S saves a version, Ctrl+Shift+F toggles zen mode. */
export const useNoteEditorKeys = (keys: NoteEditorKeys) => {
  useEventListener(window, "keydown", (event: KeyboardEvent) => {
    if (!toValue(keys.isEditing)) return;

    const key = event.key.toLowerCase();

    if (isPlainMod(event) && key === "s") {
      event.preventDefault();
      keys.saveVersion();
      return;
    }

    if (isShiftMod(event) && key === "f") {
      event.preventDefault();
      keys.toggleZen();
      return;
    }

    if (key === "escape" && toValue(keys.isZen) && !escapeIsTaken(event)) keys.exitZen();
  });
};
