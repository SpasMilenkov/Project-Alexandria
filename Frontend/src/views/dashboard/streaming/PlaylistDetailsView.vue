<template>
  <div class="px-2 md:px-6 py-5">
    <!-- Back -->
    <UButton
      icon="mdi:arrow-left"
      label="Playlists"
      color="neutral"
      variant="ghost"
      size="sm"
      class="mb-4 -ml-1"
      @click="router.push('/streaming/playlists')"
    />

    <!-- Loading -->
    <div v-if="detailQuery.status.value === 'pending'" class="flex justify-center py-16">
      <UIcon name="mdi:loading" class="w-6 h-6 animate-spin text-gray-500 dark:text-gray-500" />
    </div>

    <UAlert
      v-else-if="detailQuery.status.value === 'error'"
      icon="mdi:alert-circle-outline"
      color="error"
      variant="subtle"
      title="Failed to load playlist"
    />

    <template v-else-if="playlist">
      <!-- Header card with blurred cover background -->
      <div
        class="relative rounded-2xl overflow-hidden mb-6 border border-gray-200/70 dark:border-gray-700/70"
      >
        <!-- Background: blurred cover image or ambient tint -->
        <div class="absolute inset-0">
          <img
            v-if="coverUrl && !coverErrored"
            :src="coverUrl"
            :alt="playlist.name"
            class="w-full h-full object-cover blur-sm scale-125 opacity-25 dark:opacity-15"
            @error="coverErrored = true"
          />
          <div
            v-else-if="playlist.ambientTheme"
            class="w-full h-full"
            :style="{ backgroundColor: playlist.ambientTheme, opacity: 0.08 }"
          />
        </div>
        <!-- <div class="absolute inset-0 bg-white/60 dark:bg-gray-950/70" /> -->

        <!-- Content -->
        <div class="relative flex flex-col md:flex-row items-center md:items-end gap-6 p-5 md:p-6">
          <!-- Cover image -->
          <div
            class="w-48 shrink-0 aspect-square rounded-xl overflow-hidden shadow-lg"
            :style="coverShadowStyle"
          >
            <img
              v-if="coverUrl && !coverErrored"
              :src="coverUrl"
              :alt="playlist.name"
              class="w-full h-full object-cover"
              @error="coverErrored = true"
            />
            <div v-else class="w-full h-full">
              <PlaylistCover :playlist="playlist" />
            </div>
          </div>

          <!-- Info + actions -->
          <div class="flex-1 min-w-0 w-full text-center md:text-left">
            <h1 class="text-2xl md:text-3xl font-bold text-gray-900 dark:text-gray-100">
              {{ playlist.name }}
            </h1>
            <p
              v-if="playlist.description"
              class="mt-1 max-w-xl mx-auto md:mx-0 text-sm text-gray-600 dark:text-gray-400"
            >
              {{ playlist.description }}
            </p>

            <!-- Meta: count and now playing state -->
            <div
              class="flex flex-wrap items-center justify-center md:justify-start gap-x-4 gap-y-2 mt-2 text-sm text-gray-500 dark:text-gray-500"
            >
              <span>{{ itemCountLabel }}</span>
              <span
                v-if="isCurrentPlaylist"
                class="inline-flex items-center gap-1 font-medium text-gray-600 dark:text-gray-400"
              >
                <UIcon name="mdi:volume-high" class="w-3.5 h-3.5 text-primary" />
                Now playing
              </span>
            </div>

            <!-- Actions: one primary, one utility, one overflow menu -->
            <div class="flex items-center justify-center md:justify-start gap-2 mt-4">
              <UTooltip text="Play all">
                <UButton
                  icon="mdi:play"
                  color="primary"
                  variant="solid"
                  square
                  aria-label="Play all"
                  class="size-14 rounded-full shadow-lg transition-transform hover:scale-105"
                  :ui="{ leadingIcon: 'size-6' }"
                  :loading="isLoadingQueue"
                  :disabled="!localItems.length"
                  @click="playAll"
                />
              </UTooltip>

              <UTooltip v-if="!isMobile" text="Shuffle">
                <UButton
                  icon="mdi:shuffle-variant"
                  color="neutral"
                  variant="ghost"
                  square
                  aria-label="Shuffle"
                  class="size-10 rounded-full"
                  :disabled="!localItems.length || isLoadingQueue"
                  @click="shuffleAll"
                />
              </UTooltip>

              <UDropdownMenu :items="menuItems" :content="{ align: 'start' }">
                <UButton
                  icon="mdi:dots-horizontal"
                  color="neutral"
                  variant="ghost"
                  square
                  aria-label="More actions"
                  class="size-10 rounded-full"
                />
              </UDropdownMenu>
            </div>
          </div>
        </div>
      </div>

      <!-- List toolbar -->
      <div v-if="localItems.length" class="flex items-center justify-between mb-2">
        <h2 class="text-sm font-semibold text-gray-600 dark:text-gray-400">Items</h2>
        <UButton
          :icon="addTracksIcon"
          :label="addTracksLabel"
          color="neutral"
          variant="outline"
          size="sm"
          @click="showSearch = !showSearch"
        />
      </div>

      <!-- Track search panel -->
      <div
        v-if="showSearch"
        class="mb-6 p-4 rounded-xl border border-gray-200/70 dark:border-gray-700/70 frosted-glass glass-surface"
      >
        <p class="text-sm font-medium text-gray-900 dark:text-gray-100 mb-3">Add tracks</p>
        <PlaylistTrackSearch
          :adding-ids="addingIds"
          :added-ids="addedItemJobIds"
          @add="handleAddItem"
        />
      </div>

      <!-- Empty state -->
      <div
        v-if="!localItems.length"
        class="flex flex-col items-center justify-center py-16 text-center border border-dashed border-gray-200/70 dark:border-gray-700/70 rounded-xl"
      >
        <UIcon name="mdi:music-note-off" class="w-12 h-12 text-gray-400 dark:text-gray-600 mb-3" />
        <p class="font-medium text-gray-900 dark:text-gray-100">No items in this playlist</p>
        <p class="text-sm text-gray-600 dark:text-gray-400 mt-1 mb-4">
          Add tracks from your library to start listening.
        </p>
        <UButton
          :icon="addTracksIcon"
          :label="addTracksLabel"
          color="primary"
          variant="solid"
          size="sm"
          @click="showSearch = !showSearch"
        />
      </div>

      <!-- Item list with ambient color tint -->
      <div
        v-else
        class="relative rounded-xl overflow-hidden border border-gray-200/70 dark:border-gray-700/70"
      >
        <!-- Ambient color background overlay -->
        <div
          v-if="playlist.ambientTheme"
          class="absolute inset-0 pointer-events-none"
          :style="{ backgroundColor: playlist.ambientTheme, opacity: ambientListOpacity }"
        />
        <!-- Ambient top accent line -->
        <div
          v-if="playlist.ambientTheme"
          class="absolute top-0 inset-x-0 h-0.5 pointer-events-none z-10"
          :style="{ backgroundColor: playlist.ambientTheme, opacity: 0.4 }"
        />

        <div class="relative flex flex-col divide-y divide-gray-100/50 dark:divide-gray-800/50">
          <PlaylistItemRow
            v-for="item in localItems"
            :key="item.id"
            :item="item"
            :dragged-item-id="draggedItemId"
            :is-playing="isItemPlaying(item)"
            @remove="confirmRemoveItem(item.id)"
            @drag-start="onDragStart"
            @drag-end="onDragEnd"
            @drag-over="onDragOver"
            @drop="onDrop"
            @play="playFromItem(item.id, item.fileId)"
          />
        </div>
      </div>

      <!-- Reorder saving indicator -->
      <p
        v-if="isReorderingPlaylistItem"
        class="text-xs text-gray-500 dark:text-gray-500 text-center mt-2"
      >
        Saving order...
      </p>
    </template>
  </div>

  <!-- Edit modal -->
  <UModal
    v-model:open="showEditModal"
    title="Edit playlist"
    description="Update the details and artwork for your collection."
    :ui="playlistModalUi"
  >
    <template #body>
      <PlaylistForm
        v-if="playlist"
        :initial="playlist as any"
        :loading="isUpdating"
        @submit="handleUpdate"
        @cancel="showEditModal = false"
      />
    </template>
  </UModal>

  <!-- Delete confirm modal -->
  <UModal
    v-model:open="showDeleteModal"
    title="Delete Playlist"
    :ui="{ content: glassModalContent }"
  >
    <template #body>
      <p class="text-sm text-default">
        Are you sure you want to delete
        <span class="font-semibold">{{ playlist?.name }}</span
        >? This cannot be undone.
      </p>
    </template>
    <template #footer>
      <div class="flex justify-end gap-2">
        <UButton
          label="Cancel"
          color="neutral"
          variant="outline"
          @click="showDeleteModal = false"
        />
        <UButton
          label="Delete"
          color="error"
          variant="solid"
          :loading="isDeletingPlaylist"
          @click="handleDelete"
        />
      </div>
    </template>
  </UModal>

  <!-- Remove item confirm modal -->
  <UModal
    v-model:open="showRemoveItemModal"
    title="Remove Item"
    :ui="{ content: glassModalContent }"
  >
    <template #body>
      <p class="text-sm text-default">Remove this item from the playlist?</p>
    </template>
    <template #footer>
      <div class="flex justify-end gap-2">
        <UButton
          label="Cancel"
          color="neutral"
          variant="outline"
          @click="showRemoveItemModal = false"
        />
        <UButton
          label="Remove"
          color="error"
          variant="solid"
          :loading="isRemovingPlaylistItem"
          @click="handleRemoveItem"
        />
      </div>
    </template>
  </UModal>
</template>

<script setup lang="ts">
import type { DropdownMenuItem } from "@nuxt/ui";

import { useQuery } from "@pinia/colada";
import { breakpointsTailwind, useBreakpoints } from "@vueuse/core";
import { computed, ref, watch } from "vue";
import { useRoute, useRouter } from "vue-router";

import type { PlaylistItemResponse } from "@/api/playlist";
import type { UpdatePlaylistSchema } from "@/schemas/playlist";

import { fileApi } from "@/api/file";
import { playlistApi } from "@/api/playlist";
import PlaylistCover from "@/components/streaming/PlaylistCover.vue";
import PlaylistForm from "@/components/streaming/PlaylistForm.vue";
import PlaylistItemRow from "@/components/streaming/PlaylistItemRow.vue";
import PlaylistTrackSearch from "@/components/streaming/PlaylistTrackSearch.vue";
import { useTheme } from "@/composables/useTheme";
import {
  addPlaylistItem,
  deletePlaylist,
  removePlaylistItem,
  reorderPlaylistItems,
  updatePlaylist,
} from "@/mutations/playlists";
import { PLAYLIST_QUERY_KEYS } from "@/queries/playlist";
import { usePlayerStore } from "@/stores/stream-player";
import { glassModalContent, playlistModalUi } from "@/utils/modalUi";
import { findPlaylistItemIndex, loadPlaylistAnchorPage } from "@/utils/player-source";

const store = usePlayerStore();
const toast = useToast();
const { isDark } = useTheme();

// Below md the header keeps only the play button and the overflow menu.
const isMobile = useBreakpoints(breakpointsTailwind).smaller("md");

const isLoadingQueue = ref(false);
const isAddingAllToQueue = ref(false);

const addAllToQueue = async () => {
  if (isAddingAllToQueue.value) return;
  isAddingAllToQueue.value = true;
  try {
    const total = await store.addPlaylistToQueue(playlistId.value, playlist.value?.name ?? null);
    if (total === 0) {
      toast.add({ title: "Playlist is empty", color: "warning" });
      return;
    }
    toast.add({
      title: `Added ${total} track${total === 1 ? "" : "s"} to queue`,
      color: "success",
    });
  } catch {
    toast.add({ title: "Failed to queue playlist", color: "error" });
  } finally {
    isAddingAllToQueue.value = false;
  }
};

const playAll = async () => {
  isLoadingQueue.value = true;
  try {
    await store.startContext(
      { isVideo: false, playlistId: playlistId.value },
      { label: playlist.value?.name ?? "Playlist" },
    );
  } finally {
    isLoadingQueue.value = false;
  }
};

const shuffleAll = async () => {
  isLoadingQueue.value = true;
  try {
    await store.startContext(
      { isVideo: false, playlistId: playlistId.value },
      { label: playlist.value?.name ?? "Playlist", shuffle: true },
    );
  } finally {
    isLoadingQueue.value = false;
  }
};

const route = useRoute();
const router = useRouter();

const playlistId = computed(() => route.params.id as string);

// The playlist this view shows is the player's source while its context
// points at it, so the header keeps a playing state after loading finishes.
const isCurrentPlaylist = computed(() => store.context?.ref.playlistId === playlistId.value);

// Identity of the now-playing file: the playlist occurrence when it has one,
// otherwise the file itself. Matched against row ids so the row the audio is
// coming from stays highlighted even when queued rather than context-played.
const playingIdentity = computed(() => {
  const file = store.nowPlaying?.file ?? null;
  if (!file) return null;
  return file.playlistItemId ?? file.fileId;
});

const isItemPlaying = (item: PlaylistItemResponse): boolean => {
  const identity = playingIdentity.value;
  if (!identity) return false;
  return item.id === identity || item.fileId === identity;
};

const detailQuery = useQuery({
  key: () => PLAYLIST_QUERY_KEYS.detail(playlistId.value),
  query: () => playlistApi.getById(playlistId.value),
  staleTime: 30_000,
});

const playlist = computed(() => detailQuery.data.value ?? null);

const coverUrl = computed(() =>
  playlist.value?.hasCover
    ? playlistApi.getPlaylistCoverUrl(playlistId.value, playlist.value.updatedAt)
    : null,
);
const coverErrored = ref(false);

// A fresh upload bumps updatedAt (new ?v= cache-buster), so drop any latched
// error then instead of hiding a cover that exists now.
watch(
  () => [playlistId.value, playlist.value?.updatedAt],
  () => {
    coverErrored.value = false;
  },
);

// Ambient color helpers
const coverShadowStyle = computed(() => {
  if (!playlist.value?.ambientTheme) return {};
  return {
    boxShadow: `0 8px 24px -4px color-mix(in srgb, ${playlist.value.ambientTheme} 30%, transparent)`,
  };
});

const ambientListOpacity = computed(() => (isDark.value ? 0.1 : 0.06));

const { mutateAsync: update, isLoading: isUpdating, state: updateState } = updatePlaylist();
const { mutateAsync: remove, isLoading: isDeletingPlaylist, state: deleteState } = deletePlaylist();
const { mutateAsync: addItem } = addPlaylistItem();
const {
  mutateAsync: removeItem,
  isLoading: isRemovingPlaylistItem,
  state: removeItemState,
} = removePlaylistItem();
const {
  mutateAsync: reorder,
  isLoading: isReorderingPlaylistItem,
  state: reorderState,
} = reorderPlaylistItems();

// Local item list
const localItems = ref<PlaylistItemResponse[]>([]);

watch(
  () => playlist.value?.items,
  (items) => {
    if (items) localItems.value = [...items];
  },
  { immediate: true },
);

const addedItemJobIds = computed(() => new Set(localItems.value.map((i) => i.transpilationJobId)));

const addingIds = ref<Set<string>>(new Set());

// Modal state
const showSearch = ref(false);
const showEditModal = ref(false);
const showDeleteModal = ref(false);
const showRemoveItemModal = ref(false);
const removeItemTarget = ref<string | null>(null);

// Header display helpers
const itemCountLabel = computed(() => {
  const count = localItems.value.length;
  return `${count} ${count === 1 ? "item" : "items"}`;
});

const addTracksIcon = computed(() => (showSearch.value ? "mdi:close" : "mdi:plus"));
const addTracksLabel = computed(() => (showSearch.value ? "Cancel" : "Add tracks"));

// Overflow menu: secondary actions first, destructive action in its own group.
// Shuffle only lives here on mobile, where the header shows a single action.
const menuItems = computed<DropdownMenuItem[][]>(() => {
  const isEmpty = !localItems.value.length;
  const secondary: DropdownMenuItem[] = [];

  if (isMobile.value) {
    secondary.push({
      label: "Shuffle",
      icon: "mdi:shuffle-variant",
      disabled: isEmpty || isLoadingQueue.value,
      onSelect: shuffleAll,
    });
  }

  secondary.push(
    {
      label: "Add all to queue",
      icon: "mdi:playlist-plus",
      disabled: isEmpty || isAddingAllToQueue.value,
      onSelect: addAllToQueue,
    },
    {
      label: "Edit playlist",
      icon: "mdi:pencil",
      onSelect: () => {
        showEditModal.value = true;
      },
    },
  );

  const destructive: DropdownMenuItem[] = [
    {
      label: "Delete playlist",
      icon: "mdi:trash-can-outline",
      color: "error",
      onSelect: () => {
        showDeleteModal.value = true;
      },
    },
  ];

  return [secondary, destructive];
});

// Drag-and-drop state
const draggedItemId = ref<string | null>(null);

const playFromItem = async (itemId: string, fileId: string) => {
  isLoadingQueue.value = true;
  const descriptor = { isVideo: false, playlistId: playlistId.value };
  try {
    const result = await loadPlaylistAnchorPage(playlistId.value, fileId, itemId);
    const index = findPlaylistItemIndex(result.items, itemId);
    if (index === -1) {
      toast.add({ title: "Track is no longer in this playlist", color: "warning" });
      return;
    }
    const file = result.items[index];
    if (!file) {
      toast.add({ title: "Track is no longer in this playlist", color: "warning" });
      return;
    }
    await store.playTrackInContext(file, descriptor, playlist.value?.name ?? "Playlist");
  } catch {
    toast.add({ title: "Failed to load playlist", color: "error" });
  } finally {
    isLoadingQueue.value = false;
  }
};

const onDragStart = (id: string) => {
  draggedItemId.value = id;
};

const onDragEnd = () => {
  draggedItemId.value = null;
};

const onDragOver = (targetId: string) => {
  if (!draggedItemId.value || draggedItemId.value === targetId) return;

  const items = [...localItems.value];
  const fromIndex = items.findIndex((i) => i.id === draggedItemId.value);
  const toIndex = items.findIndex((i) => i.id === targetId);
  if (fromIndex === -1 || toIndex === -1) return;

  const [moved] = items.splice(fromIndex, 1);
  items.splice(toIndex, 0, moved);
  localItems.value = items;
};

const onDrop = async (_targetId: string) => {
  draggedItemId.value = null;
  await reorder({
    playlistId: playlistId.value,
    req: { orderedItemIds: localItems.value.map((i) => i.id) },
  });
};

const handleAddItem = async (transpilationJobId: string) => {
  addingIds.value = new Set([...addingIds.value, transpilationJobId]);
  try {
    await addItem({ playlistId: playlistId.value, req: { transpilationJobId } });
  } finally {
    addingIds.value = new Set([...addingIds.value].filter((id) => id !== transpilationJobId));
  }
};

const confirmRemoveItem = (itemId: string) => {
  removeItemTarget.value = itemId;
  showRemoveItemModal.value = true;
};

const handleUpdate = async (payload: UpdatePlaylistSchema) => {
  try {
    // Upload the bytes before flagging the cover: the update bumps updatedAt
    // (new ?v= cache-buster), and a cover URL fetched before the PUT lands
    // reads a 404 that nginx then caches.
    if (payload.coverFile) {
      const { uploadUrl } = await playlistApi.getCoverUploadUrl({
        playlistId: playlistId.value,
        mimeType: payload.coverFile.type,
        fileSize: payload.coverFile.size,
      });
      await fileApi.uploadToS3(uploadUrl, payload.coverFile);
    }
    await update({
      id: playlistId.value,
      req: {
        name: payload.name,
        description: payload.description,
        hasCover: Boolean(payload.coverFile),
      },
    });
    if (!updateState.value.error) showEditModal.value = false;
  } catch {
    toast.add({ title: "Failed to update playlist", color: "error" });
  }
};

const handleDelete = async () => {
  await remove(playlistId.value);
  if (!deleteState.value.error) router.push("/streaming/playlists");
};

const handleRemoveItem = async () => {
  if (!removeItemTarget.value) return;
  await removeItem({ playlistId: playlistId.value, itemId: removeItemTarget.value });
  if (!removeItemState.value.error) {
    showRemoveItemModal.value = false;
    removeItemTarget.value = null;
  }
};
</script>
