import { computed, shallowRef, watch } from "vue";

import type { HomeEditorTarget, HomeSettings } from "@/types/home";

import { cloneHomeSettings } from "@/utils/home-layout";

interface EditorContext {
  target: HomeEditorTarget;
  selectedId: string | null;
}

interface HistorySnapshot {
  settings: HomeSettings;
  context: EditorContext;
}

interface HistoryEntry {
  before: HistorySnapshot;
  after: HistorySnapshot;
}

interface HistoryOptions {
  session: () => string;
  read: () => HomeSettings | null;
  write: (settings: HomeSettings) => void;
  context: () => EditorContext;
  restoreContext: (context: EditorContext) => void;
}

const HISTORY_LIMIT = 100;

export const useHomeEditorHistory = (options: HistoryOptions) => {
  const past = shallowRef<HistoryEntry[]>([]);
  const future = shallowRef<HistoryEntry[]>([]);
  let activeGroup: string | undefined = undefined;

  const endGroup = () => {
    activeGroup = undefined;
  };

  const clear = () => {
    past.value = [];
    future.value = [];
    endGroup();
  };

  watch(options.session, clear, { flush: "sync" });

  const snapshot = (): HistorySnapshot | null => {
    const settings = options.read();

    if (!settings) return null;

    return { settings: cloneHomeSettings(settings), context: { ...options.context() } };
  };

  const record = (action: () => void, group?: string) => {
    const before = snapshot();

    if (!before) return;

    action();

    const after = snapshot();

    if (!after || JSON.stringify(before.settings) === JSON.stringify(after.settings)) return;

    const previous = past.value[past.value.length - 1];

    if (group && activeGroup === group && previous) {
      past.value = [...past.value.slice(0, -1), { before: previous.before, after }];
    } else {
      past.value = [...past.value, { before, after }].slice(-HISTORY_LIMIT);
    }

    future.value = [];
    activeGroup = group;
  };

  const restore = (value: HistorySnapshot) => {
    endGroup();
    options.write(cloneHomeSettings(value.settings));
    options.restoreContext({ ...value.context });
  };

  const undo = () => {
    const entry = past.value[past.value.length - 1];

    if (!entry) return false;

    past.value = past.value.slice(0, -1);
    future.value = [...future.value, entry];
    restore(entry.before);

    return true;
  };

  const redo = () => {
    const entry = future.value[future.value.length - 1];

    if (!entry) return false;

    future.value = future.value.slice(0, -1);
    past.value = [...past.value, entry];
    restore(entry.after);

    return true;
  };

  return {
    canUndo: computed(() => past.value.length > 0),
    canRedo: computed(() => future.value.length > 0),
    record,
    undo,
    redo,
    endGroup,
  };
};
