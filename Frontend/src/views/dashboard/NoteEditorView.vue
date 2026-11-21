<script setup lang="ts">
import { useQuery } from "@pinia/colada";
import { refDebounced, useLocalStorage } from "@vueuse/core";
import { computed, ref, useTemplateRef, watch } from "vue";
import { useRoute } from "vue-router";

import { isAuthError } from "@/api/shuffle";
import BlockSpinner from "@/components/common/BlockSpinner.vue";
import GlassEmptyState from "@/components/common/GlassEmptyState.vue";
import SegmentedControl from "@/components/dashboard/integrations/SegmentedControl.vue";
import NoteConnections from "@/components/notes/NoteConnections.vue";
import NoteEditor from "@/components/notes/NoteEditor.vue";
import NoteOutline from "@/components/notes/NoteOutline.vue";
import { useAppToast } from "@/composables/useAppToast";
import { NOTE_EDITOR_MODE_OPTIONS, type NoteEditorMode } from "@/features/notes/editor-modes";
import { extractHeadings } from "@/features/notes/outline";
import { survivesRoundTrip } from "@/features/notes/round-trip";
import { countWords } from "@/features/notes/stats";
import { getFile } from "@/queries/files";
import { getNoteVersionText } from "@/queries/notes";
import { isMarkdownMimeType } from "@/utils/files/mimetype";
import { formatBytes } from "@/utils/size";

// Past this size Tiptap gets sluggish on every keystroke, and notes are rarely this long.
const MAX_NOTE_BYTES = 1_000_000;

interface Blocker {
  icon: string;
  title: string;
  description: string;
}

const route = useRoute();
const clipboardToast = useAppToast("clipboard");

const fileId = computed(() => String(route.params.fileId ?? ""));

const {
  data: file,
  isPending: isFilePending,
  error: fileError,
  refetch: refetchFile,
} = useQuery(() => ({ ...getFile(fileId.value), enabled: fileId.value.length > 0 }));

// The editor stays on the version it opened. Following a newer version would throw away what
// is being typed, and choosing between the two belongs to the save flow.
const openedVersionId = ref<string | null>(null);
const draft = ref("");
const loadedVersionId = ref<string | null>(null);
const copied = ref(false);
const mode = ref<NoteEditorMode>("rich");

const noteEditor = useTemplateRef<{ scrollToHeading: (index: number) => void }>("noteEditor");

// A layout preference for this browser only, like the gallery viewer's info panel.
const isOutlineOpen = useLocalStorage("notes-outline-open", true);
const isConnectionsOpen = useLocalStorage("notes-connections-open", true);

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
const OUTLINE_CLOSED = "opacity-0 invisible";
const CONNECTIONS_CLOSED = "lg:opacity-0 lg:invisible";

const outlinePanelState = computed(() => {
  if (isOutlineOpen.value) return "opacity-100 visible";

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

const setOutlineOpen = (open: boolean) => {
  isOutlineOpen.value = open;
};

const setConnectionsOpen = (open: boolean) => {
  isConnectionsOpen.value = open;
};

// The outline trails typing slightly, so long notes aren't re-read on every keystroke.
const settledDraft = refDebounced(draft, 250);
const headings = computed(() => extractHeadings(settledDraft.value));
const wordCount = computed(() => countWords(settledDraft.value));
const activeHeading = ref<number | null>(null);

const setActiveHeading = (index: number | null) => {
  activeHeading.value = index;
};

watch(fileId, () => {
  openedVersionId.value = null;
  loadedVersionId.value = null;
  draft.value = "";
  mode.value = "rich";
  activeHeading.value = null;
});

watch(
  () => file.value?.currentVersion.id,
  (versionId) => {
    if (versionId && !openedVersionId.value) openedVersionId.value = versionId;
  },
  { immediate: true },
);

const blocker = computed((): Blocker | null => {
  const current = file.value;

  if (!current) return null;

  const version = current.currentVersion;

  if (current.deletedAt) {
    return {
      description: "Restore it from the trash to open it again.",
      icon: "i-lucide-file-x",
      title: "This note is in the trash",
    };
  }

  if (!isMarkdownMimeType(version.mimeType)) {
    return {
      description: "Only markdown files open in the note editor.",
      icon: "i-lucide-file-x",
      title: "Not a markdown file",
    };
  }

  if (version.isEncrypted) {
    return {
      description: "Encrypted notes need a password step the editor doesn't have yet.",
      icon: "i-lucide-lock",
      title: "Encrypted notes can't be opened yet",
    };
  }

  const size = Number(version.size);

  if (size > MAX_NOTE_BYTES) {
    return {
      description: `The editor opens notes up to ${formatBytes(MAX_NOTE_BYTES)}, and this one is ${formatBytes(size)}.`,
      icon: "i-lucide-file-warning",
      title: "Too large for the editor",
    };
  }

  return null;
});

const canReadText = computed(() => openedVersionId.value !== null && blocker.value === null);

const {
  data: original,
  isPending: isTextPending,
  error: textError,
  refetch: refetchText,
} = useQuery(() => ({
  ...getNoteVersionText(openedVersionId.value ?? ""),
  enabled: canReadText.value,
}));

watch(
  original,
  (text) => {
    const versionId = openedVersionId.value;

    if (text === undefined || !versionId || loadedVersionId.value === versionId) return;

    draft.value = text;
    loadedVersionId.value = versionId;
  },
  { immediate: true },
);

const isLoaded = computed(
  () => loadedVersionId.value !== null && loadedVersionId.value === openedVersionId.value,
);

// Compared after normalising, so switching modes or undoing back to the start doesn't count.
const isDirty = computed(() => {
  if (!isLoaded.value || original.value === undefined) return false;

  return !survivesRoundTrip(original.value, draft.value);
});

const isPending = computed(() => {
  if (isFilePending.value) return true;

  return canReadText.value && isTextPending.value;
});

const visibleError = computed(() => {
  const error = fileError.value ?? textError.value;

  if (!error || isAuthError(error)) return null;

  return error;
});

const title = computed(() => file.value?.fileName ?? "Note");

const isEditing = computed(
  () => isLoaded.value && original.value !== undefined && blocker.value === null,
);

const editedAt = computed(() => file.value?.updatedAt ?? file.value?.createdAt ?? "");

const jumpToHeading = (index: number) => {
  noteEditor.value?.scrollToHeading(index);
};

const folderRoute = computed(() => {
  const directoryId = file.value?.directoryId;

  if (directoryId) return { name: "dashboard", params: { dirId: directoryId } };

  return { name: "dashboard" };
});

const retry = () => {
  if (fileError.value) {
    void refetchFile();
    return;
  }

  void refetchText();
};

const copyMarkdown = async () => {
  try {
    await navigator.clipboard.writeText(draft.value);
    copied.value = true;
    setTimeout(() => (copied.value = false), 1500);
  } catch (error) {
    clipboardToast.error("Couldn't copy the markdown", error);
  }
};
</script>

<template>
  <!-- On desktop the desk fills the visible area and each column scrolls on its own. -->
  <div class="flex min-h-full w-full flex-col gap-3 p-3 lg:h-full">
    <header
      class="flex shrink-0 flex-wrap items-center justify-between gap-3 rounded-xl border border-gray-200/70 px-4 py-3 frosted-glass glass-surface-strong md:px-5 dark:border-gray-700/70"
    >
      <div class="flex min-w-0 items-center gap-2.5">
        <UButton
          icon="i-lucide-arrow-left"
          color="neutral"
          variant="ghost"
          square
          class="justify-center"
          aria-label="Back to the folder"
          :to="folderRoute"
        />

        <UIcon
          name="i-lucide-notebook-pen"
          class="h-5 w-5 shrink-0 text-gray-600 dark:text-gray-400"
        />

        <h1 class="truncate text-xl font-semibold text-gray-900 dark:text-gray-100">
          {{ title }}
        </h1>
      </div>

      <div v-if="isEditing" class="flex flex-wrap items-center gap-2">
        <UBadge v-if="isDirty" color="neutral" variant="subtle" label="Unsaved changes" />

        <SegmentedControl v-model="mode" :options="NOTE_EDITOR_MODE_OPTIONS" label="Editor mode" />

        <UTooltip text="Copy markdown">
          <UButton
            :icon="copied ? 'i-lucide-check' : 'i-lucide-copy'"
            :color="copied ? 'success' : 'neutral'"
            variant="ghost"
            square
            class="justify-center"
            aria-label="Copy markdown"
            @click="copyMarkdown"
          />
        </UTooltip>

        <!-- Only collapsed panels get a button here; open ones collapse from their own header. -->
        <Transition
          enter-active-class="transition-opacity duration-200 ease-out motion-reduce:transition-none"
          enter-from-class="opacity-0"
          leave-active-class="transition-opacity duration-150 ease-in motion-reduce:transition-none"
          leave-to-class="opacity-0"
        >
          <span v-if="!isOutlineOpen" class="hidden lg:inline-flex">
            <UTooltip text="Show outline">
              <UButton
                icon="i-lucide-panel-left-open"
                color="neutral"
                variant="ghost"
                square
                class="justify-center"
                aria-label="Show outline"
                @click="setOutlineOpen(true)"
              />
            </UTooltip>
          </span>
        </Transition>

        <Transition
          enter-active-class="transition-opacity duration-200 ease-out motion-reduce:transition-none"
          enter-from-class="opacity-0"
          leave-active-class="transition-opacity duration-150 ease-in motion-reduce:transition-none"
          leave-to-class="opacity-0"
        >
          <span v-if="!isConnectionsOpen" class="hidden lg:inline-flex">
            <UTooltip text="Show connections">
              <UButton
                icon="i-lucide-panel-right-open"
                color="neutral"
                variant="ghost"
                square
                class="justify-center"
                aria-label="Show connections"
                @click="setConnectionsOpen(true)"
              />
            </UTooltip>
          </span>
        </Transition>
      </div>
    </header>

    <div v-if="isPending" class="flex justify-center py-24">
      <BlockSpinner :size="24" aria-label="Loading the note" class="text-gray-500" />
    </div>

    <div v-else-if="visibleError" class="flex py-16">
      <GlassEmptyState
        icon="i-lucide-triangle-alert"
        title="Couldn't open the note"
        :description="visibleError.message"
        class="m-auto"
      >
        <UButton color="neutral" variant="outline" class="rounded-full px-5" @click="retry">
          Try again
        </UButton>
      </GlassEmptyState>
    </div>

    <div v-else-if="blocker" class="flex py-16">
      <GlassEmptyState
        :icon="blocker.icon"
        :title="blocker.title"
        :description="blocker.description"
        class="m-auto"
      >
        <UButton
          color="neutral"
          variant="outline"
          class="rounded-full px-5"
          label="Back to the folder"
          :to="folderRoute"
        />
      </GlassEmptyState>
    </div>

    <div
      v-else-if="isEditing && original !== undefined && openedVersionId && file"
      class="grid grid-cols-1 items-start gap-3 transition-[grid-template-columns] duration-200 ease-out motion-reduce:transition-none lg:min-h-0 lg:flex-1 lg:grid-rows-[minmax(0,1fr)] lg:items-stretch lg:gap-0"
      :class="deskColumns"
    >
      <!-- The panel keeps its own width inside the shrinking track, so it slides out of view
           instead of squeezing its text. -->
      <aside class="hidden lg:flex lg:min-h-0 lg:min-w-0 lg:flex-col lg:overflow-hidden">
        <div
          class="flex min-h-0 w-[13.75rem] flex-col pr-3 transition-[opacity,visibility] duration-200 ease-out motion-reduce:transition-none"
          :class="outlinePanelState"
        >
          <NoteOutline
            :headings="headings"
            :can-jump="mode === 'rich'"
            :active-index="isOutlineOpen ? activeHeading : null"
            @select="jumpToHeading"
            @collapse="setOutlineOpen(false)"
          />
        </div>
      </aside>

      <NoteEditor
        ref="noteEditor"
        :key="openedVersionId"
        v-model="draft"
        v-model:mode="mode"
        :source="original"
        :headings="headings"
        class="lg:min-h-0 lg:overflow-y-auto"
        @active-heading="setActiveHeading"
      />

      <!-- Below desktop width the connections sit under the sheet and are always shown. -->
      <aside class="lg:min-h-0 lg:min-w-0 lg:overflow-hidden">
        <div
          class="transition-[opacity,visibility] duration-200 ease-out motion-reduce:transition-none lg:max-h-full lg:w-[17.75rem] lg:overflow-y-auto lg:pl-3"
          :class="connectionsPanelState"
        >
          <NoteConnections
            :file-id="file.fileId"
            :directory-id="file.directoryId"
            :version-number="file.currentVersion.versionNumber"
            :size-bytes="Number(file.currentVersion.size)"
            :edited-at="editedAt"
            :word-count="wordCount"
            @collapse="setConnectionsOpen(false)"
          />
        </div>
      </aside>
    </div>
  </div>
</template>
