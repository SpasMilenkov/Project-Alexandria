import { useLocalStorage } from "@vueuse/core";
import { computed, ref } from "vue";

import { useHideAppChrome } from "@/composables/useAppChrome";

// Full class literals, so Tailwind sees every column layout at build time. The desk always has
// three tracks so the browser can animate between them: a collapsed panel's track shrinks to
// zero and the sheet grows into the space. Each side track includes its 0.75rem gap, because a
// grid gap would stay behind next to an empty track.
const DESK_COLUMNS = {
  both: "lg:grid-cols-[13.75rem_minmax(0,1fr)_17.75rem]",
  connections: "lg:grid-cols-[0rem_minmax(0,1fr)_17.75rem]",
  none: "lg:grid-cols-[0rem_minmax(0,1fr)_0rem]",
  outline: "lg:grid-cols-[13.75rem_minmax(0,1fr)_0rem]",
} as const;

// Panels fade as their track closes and end up invisible, which also takes them out of the tab
// order while collapsed. Connections only collapse on desktop; below it they sit under the sheet.
const OUTLINE_OPEN = "opacity-100 visible";
const OUTLINE_CLOSED = "opacity-0 invisible";
const CONNECTIONS_CLOSED = "lg:opacity-0 lg:invisible";

/**
 * The note editor's page layout: the outline and connections panels beside the sheet, and zen
 * mode, which clears the app's navigation and background picture away from the note. The
 * panels are context for the note, so zen leaves them as they are.
 */
export const useNoteDesk = () => {
  // A layout preference for this browser only, like the gallery viewer's info panel.
  const isOutlineOpen = useLocalStorage("notes-outline-open", true);
  const isConnectionsOpen = useLocalStorage("notes-connections-open", true);

  // Not remembered: every note opens with zen off.
  const isZen = ref(false);

  useHideAppChrome(isZen);

  const outlinePanelState = computed(() => {
    if (isOutlineOpen.value) return OUTLINE_OPEN;

    return OUTLINE_CLOSED;
  });

  const connectionsPanelState = computed(() => {
    if (isConnectionsOpen.value) return "";

    return CONNECTIONS_CLOSED;
  });

  const deskColumns = computed(() => {
    if (isOutlineOpen.value && isConnectionsOpen.value) return DESK_COLUMNS.both;
    if (isOutlineOpen.value) return DESK_COLUMNS.outline;
    if (isConnectionsOpen.value) return DESK_COLUMNS.connections;

    return DESK_COLUMNS.none;
  });

  // With nothing beside it the sheet spans the whole desk, and lines that long are hard to read.
  const isSheetAlone = computed(() => !isOutlineOpen.value && !isConnectionsOpen.value);

  const setOutlineOpen = (open: boolean) => {
    isOutlineOpen.value = open;
  };

  const setConnectionsOpen = (open: boolean) => {
    isConnectionsOpen.value = open;
  };

  const toggleZen = () => {
    isZen.value = !isZen.value;
  };

  const exitZen = () => {
    isZen.value = false;
  };

  return {
    connectionsPanelState,
    deskColumns,
    exitZen,
    isConnectionsOpen,
    isOutlineOpen,
    isSheetAlone,
    isZen,
    outlinePanelState,
    setConnectionsOpen,
    setOutlineOpen,
    toggleZen,
  };
};
